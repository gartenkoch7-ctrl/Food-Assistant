using FoodAssistant.Application.Households;
using FoodAssistant.Contracts.Accounts;
using FoodAssistant.Contracts.Households;
using FoodAssistant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FoodAssistant.Infrastructure.Households;

internal sealed class HouseholdStore(AppDbContext db, TimeProvider timeProvider) : IHouseholdStore
{
    public Task<HouseholdMembershipInfo?> FindMembershipAsync(
        Guid householdId,
        Guid userId,
        CancellationToken cancellationToken) =>
        db.HouseholdMemberships
            .AsNoTracking()
            .Where(m => m.HouseholdId == householdId && m.UserId == userId)
            .Select(m => new HouseholdMembershipInfo(m.HouseholdId, m.UserId, m.Role))
            .SingleOrDefaultAsync(cancellationToken);

    // Deleted households are hidden by the NotDeleted filter of Households.
    public async Task<IReadOnlyList<Guid>> ListActiveHouseholdIdsAsync(CancellationToken cancellationToken) =>
        await db.Households
            .AsNoTracking()
            .OrderBy(h => h.Id)
            .Select(h => h.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<MeHouseholdResponse>> GetHouseholdsOfUserAsync(
        Guid userId,
        CancellationToken cancellationToken) =>
        await db.HouseholdMemberships
            .AsNoTracking()
            .Where(m => m.UserId == userId)
            .OrderBy(m => m.Household!.Name)
            .Select(m => new MeHouseholdResponse(m.HouseholdId, m.Household!.Name, m.Household.TimeZoneId, m.Role))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<HouseholdResponse> CreateAsync(
        NewHousehold household,
        Guid ownerUserId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(household);
        var entity = Household.CreateWithOwner(household.Name, household.TimeZoneId, ownerUserId, timeProvider.GetUtcNow());
        db.Households.Add(entity);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return new HouseholdResponse(entity.Id, entity.Name, entity.TimeZoneId);
    }

    public async Task<IReadOnlyList<HouseholdMemberResponse>> GetMembersAsync(
        Guid householdId,
        CancellationToken cancellationToken) =>
        await db.HouseholdMemberships
            .AsNoTracking()
            .Where(m => m.HouseholdId == householdId)
            .OrderBy(m => m.User!.DisplayName)
            .Select(m => new HouseholdMemberResponse(m.UserId, m.User!.DisplayName, m.Role, m.JoinedAt))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public Task<MembershipChangeOutcome> ChangeRoleAsync(
        Guid householdId,
        Guid userId,
        HouseholdRole newRole,
        CancellationToken cancellationToken) =>
        ChangeMembershipAsync(householdId, userId, newRole, cancellationToken);

    public Task<MembershipChangeOutcome> RemoveMemberAsync(
        Guid householdId,
        Guid userId,
        CancellationToken cancellationToken) =>
        ChangeMembershipAsync(householdId, userId, newRole: null, cancellationToken);

    /// <param name="newRole"><see langword="null"/> removes the membership.</param>
    private async Task<MembershipChangeOutcome> ChangeMembershipAsync(
        Guid householdId,
        Guid userId,
        HouseholdRole? newRole,
        CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        // Counting owners and changing one must not interleave with another change in the same
        // household, otherwise two owners could demote each other and leave none.
        await PostgresLocks.LockHouseholdAsync(db.Database, householdId, cancellationToken).ConfigureAwait(false);

        var membership = await db.HouseholdMemberships
            .SingleOrDefaultAsync(m => m.HouseholdId == householdId && m.UserId == userId, cancellationToken)
            .ConfigureAwait(false);
        if (membership is null)
        {
            return MembershipChangeOutcome.MemberNotFound;
        }

        var ownerCount = await db.HouseholdMemberships
            .CountAsync(m => m.HouseholdId == householdId && m.Role == HouseholdRole.Owner, cancellationToken)
            .ConfigureAwait(false);
        if (HouseholdOwnershipRules.LeavesNoOwner(membership.Role, newRole, ownerCount))
        {
            return MembershipChangeOutcome.LastOwner;
        }

        if (newRole is { } role)
        {
            membership.Role = role;
        }
        else
        {
            db.HouseholdMemberships.Remove(membership);
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return MembershipChangeOutcome.Succeeded;
    }
}
