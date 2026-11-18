using System.Linq.Expressions;
using FoodAssistant.Application.Households;
using FoodAssistant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FoodAssistant.Infrastructure.Households;

/// <summary>Invitations; the active-household query filter applies unless stated otherwise.</summary>
internal sealed class InvitationStore(AppDbContext db) : IInvitationStore
{
    private static readonly Expression<Func<Invitation, InvitationInfo>> ToInfo = i =>
        new InvitationInfo(i.Id, i.HouseholdId, i.Role, i.CreatedAt, i.ExpiresAt, i.AcceptedAt, i.RevokedAt);

    private static readonly Func<Invitation, InvitationInfo> ToInfoCompiled = ToInfo.Compile();

    public async Task<InvitationInfo> AddAsync(NewInvitation invitation, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(invitation);
        var entity = new Invitation
        {
            HouseholdId = invitation.HouseholdId,
            Role = invitation.Role,
            TokenHash = invitation.TokenHash,
            CreatedByUserId = invitation.CreatedByUserId,
            CreatedAt = invitation.CreatedAt,
            ExpiresAt = invitation.ExpiresAt,
        };
        db.Invitations.Add(entity);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ToInfoCompiled(entity);
    }

    public async Task<IReadOnlyList<InvitationInfo>> ListAsync(CancellationToken cancellationToken) =>
        await db.Invitations
            .AsNoTracking()
            .OrderByDescending(i => i.CreatedAt)
            .Select(ToInfo)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<RevokeInvitationOutcome> RevokeAsync(
        Guid invitationId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var invitation = await db.Invitations
            .SingleOrDefaultAsync(i => i.Id == invitationId, cancellationToken)
            .ConfigureAwait(false);
        if (invitation is null)
        {
            return RevokeInvitationOutcome.NotFound;
        }

        if (invitation.AcceptedAt is not null)
        {
            return RevokeInvitationOutcome.AlreadyAccepted;
        }

        if (invitation.RevokedAt is not null)
        {
            return RevokeInvitationOutcome.Revoked;
        }

        invitation.RevokedAt = now;
        try
        {
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Accepted or revoked at the same moment; report what actually happened.
            await db.Entry(invitation).ReloadAsync(cancellationToken).ConfigureAwait(false);
            return invitation.AcceptedAt is not null
                ? RevokeInvitationOutcome.AlreadyAccepted
                : RevokeInvitationOutcome.Revoked;
        }

        return RevokeInvitationOutcome.Revoked;
    }

    /// <remarks>
    /// Ignores the active-household filter on purpose: accepting happens before any household is
    /// active, and only someone holding the secret token can produce a matching hash.
    /// </remarks>
    public Task<InvitationInfo?> FindByTokenHashAsync(string tokenHash, CancellationToken cancellationToken) =>
        db.Invitations
            .AsNoTracking()
            .IgnoreQueryFilters([AppDbContext.ActiveHouseholdFilter])
            .Where(i => i.TokenHash == tokenHash)
            .Select(ToInfo)
            .SingleOrDefaultAsync(cancellationToken);
}
