using FoodAssistant.Application.Accounts;
using FoodAssistant.Application.Households;
using FoodAssistant.Infrastructure.Households;
using FoodAssistant.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FoodAssistant.Infrastructure.Accounts;

/// <summary>
/// Atomic account operations. Each runs in one transaction on the request's context; Identity's
/// <see cref="UserManager{TUser}"/> saves through the same context, so its writes are part of it.
/// </summary>
internal sealed class AccountProvisioning(
    AppDbContext db,
    UserManager<User> users,
    TimeProvider timeProvider,
    ILogger<AccountProvisioning> logger) : IAccountProvisioning
{
    public async Task<ProvisioningResult> CompleteSetupAsync(
        NewUserAccount admin,
        NewHousehold household,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(admin);
        ArgumentNullException.ThrowIfNull(household);

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await PostgresLocks.LockSetupAsync(db.Database, cancellationToken).ConfigureAwait(false);

        if (await db.Users.AnyAsync(cancellationToken).ConfigureAwait(false))
        {
            return ProvisioningResult.Failure(ProvisioningStatus.SetupAlreadyDone);
        }

        var now = timeProvider.GetUtcNow();
        var user = NewUser(admin, now, isServerAdmin: true);
        var created = await CreateUserAsync(user, admin.Password).ConfigureAwait(false);
        if (created is not null)
        {
            return created;
        }

        var entity = Household.CreateWithOwner(household.Name, household.TimeZoneId, user.Id, now);
        db.Households.Add(entity);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

        return ProvisioningResult.Success(user.Id, entity.Id, Contracts.Households.HouseholdRole.Owner);
    }

    public async Task<ProvisioningResult> CreateAccountFromInvitationAsync(
        Guid invitationId,
        NewUserAccount account,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(account);

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var now = timeProvider.GetUtcNow();
        var invitation = await FindRedeemableAsync(invitationId, now, cancellationToken).ConfigureAwait(false);
        if (invitation is null)
        {
            return ProvisioningResult.Failure(ProvisioningStatus.InvitationNotRedeemable);
        }

        var user = NewUser(account, now, isServerAdmin: false);
        var created = await CreateUserAsync(user, account.Password).ConfigureAwait(false);
        if (created is not null)
        {
            return created;
        }

        return await RedeemAsync(invitation, user.Id, now, transaction, cancellationToken).ConfigureAwait(false);
    }

    public async Task<ProvisioningResult> JoinFromInvitationAsync(
        Guid invitationId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var now = timeProvider.GetUtcNow();
        var invitation = await FindRedeemableAsync(invitationId, now, cancellationToken).ConfigureAwait(false);
        if (invitation is null || !await db.Users.AnyAsync(u => u.Id == userId, cancellationToken).ConfigureAwait(false))
        {
            return ProvisioningResult.Failure(ProvisioningStatus.InvitationNotRedeemable);
        }

        var isMember = await db.HouseholdMemberships
            .AnyAsync(m => m.HouseholdId == invitation.HouseholdId && m.UserId == userId, cancellationToken)
            .ConfigureAwait(false);
        if (isMember)
        {
            return ProvisioningResult.Failure(ProvisioningStatus.AlreadyMember);
        }

        return await RedeemAsync(invitation, userId, now, transaction, cancellationToken).ConfigureAwait(false);
    }

    private static User NewUser(NewUserAccount account, DateTimeOffset now, bool isServerAdmin) => new()
    {
        UserName = account.UserName,
        DisplayName = account.DisplayName,
        IsServerAdmin = isServerAdmin,
        CreatedAt = now,
        LockoutEnabled = true,
    };

    /// <returns><see langword="null"/> on success, otherwise the failure to return.</returns>
    private async Task<ProvisioningResult?> CreateUserAsync(User user, string password)
    {
        IdentityResult result;
        try
        {
            result = await users.CreateAsync(user, password).ConfigureAwait(false);
        }
        catch (DbUpdateException exception) when (PostgresErrors.IsUniqueViolation(exception))
        {
            // A concurrent request took the same user name between validation and insert.
            db.ChangeTracker.Clear();
            return ProvisioningResult.Failure(ProvisioningStatus.UserNameTaken);
        }

        if (result.Succeeded)
        {
            return null;
        }

        var errorCodes = string.Join(',', IdentityErrorMapping.Codes(result));
        AccountsInfrastructureLog.AccountCreationRefused(logger, errorCodes);
        db.ChangeTracker.Clear();
        return IdentityErrorMapping.ToResult(result);
    }

    /// <summary>
    /// The invitation, tracked, if it is still pending at <paramref name="now"/>. The active-household
    /// filter is ignored on purpose: no household is active while accepting, the secret token that
    /// led to <paramref name="invitationId"/> is the authorization. Deleted households stay hidden.
    /// </summary>
    private Task<Invitation?> FindRedeemableAsync(Guid invitationId, DateTimeOffset now, CancellationToken cancellationToken) =>
        db.Invitations
            .IgnoreQueryFilters([AppDbContext.ActiveHouseholdFilter])
            .Where(i => i.Id == invitationId
                        && i.AcceptedAt == null
                        && i.RevokedAt == null
                        && i.ExpiresAt > now)
            .SingleOrDefaultAsync(cancellationToken);

    private async Task<ProvisioningResult> RedeemAsync(
        Invitation invitation,
        Guid userId,
        DateTimeOffset now,
        Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction,
        CancellationToken cancellationToken)
    {
        invitation.AcceptedAt = now;
        invitation.AcceptedByUserId = userId;
        db.HouseholdMemberships.Add(new HouseholdMembership
        {
            HouseholdId = invitation.HouseholdId,
            UserId = userId,
            Role = invitation.Role,
            JoinedAt = now,
        });

        try
        {
            // xmin check on the invitation: of two concurrent redemptions only one commits.
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException)
        {
            AccountsInfrastructureLog.InvitationRedemptionConflict(logger, invitation.Id);
            db.ChangeTracker.Clear();
            return ProvisioningResult.Failure(ProvisioningStatus.InvitationNotRedeemable);
        }
        catch (DbUpdateException exception) when (PostgresErrors.IsUniqueViolation(exception))
        {
            db.ChangeTracker.Clear();
            return ProvisioningResult.Failure(ProvisioningStatus.AlreadyMember);
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return ProvisioningResult.Success(userId, invitation.HouseholdId, invitation.Role);
    }
}
