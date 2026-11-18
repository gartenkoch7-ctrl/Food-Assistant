using FoodAssistant.Application.Diagnostics;
using FoodAssistant.Contracts.Households;
using Microsoft.Extensions.Logging;

namespace FoodAssistant.Application.Households;

/// <summary>Creating, listing and revoking invitations of the active household (owners only).</summary>
public sealed class InvitationService(
    IInvitationStore store,
    IInvitationTokenService tokens,
    IHouseholdContext householdContext,
    TimeProvider timeProvider,
    ILogger<InvitationService> logger)
{
    /// <summary>Creates an invitation; the returned token is the only time it exists in clear text.</summary>
    public async Task<CreatedInvitationResponse> CreateAsync(
        Guid householdId,
        HouseholdRole role,
        Guid createdByUserId,
        CancellationToken cancellationToken)
    {
        householdContext.EnsureActive(householdId);
        if (!HouseholdRoles.IsDefined(role))
        {
            throw new ArgumentOutOfRangeException(nameof(role), "Unknown household role.");
        }

        using var activity = Telemetry.Source.StartActivity("foodassistant.households.create_invitation");

        var token = tokens.Create();
        var now = timeProvider.GetUtcNow();
        var invitation = await store
            .AddAsync(
                new NewInvitation(householdId, role, token.Hash, createdByUserId, now, now + InvitationRules.Lifetime),
                cancellationToken)
            .ConfigureAwait(false);

        HouseholdsLog.InvitationCreated(logger, invitation.Id, householdId, role);
        return new CreatedInvitationResponse(invitation.Id, token.Token, invitation.Role, invitation.ExpiresAt);
    }

    public async Task<IReadOnlyList<InvitationResponse>> ListAsync(Guid householdId, CancellationToken cancellationToken)
    {
        householdContext.EnsureActive(householdId);
        var now = timeProvider.GetUtcNow();
        var invitations = await store.ListAsync(cancellationToken).ConfigureAwait(false);
        return
        [
            .. invitations.Select(i => new InvitationResponse(
                i.Id,
                i.Role,
                InvitationRules.StatusAt(i, now),
                i.CreatedAt,
                i.ExpiresAt)),
        ];
    }

    public async Task<RevokeInvitationOutcome> RevokeAsync(
        Guid householdId,
        Guid invitationId,
        CancellationToken cancellationToken)
    {
        householdContext.EnsureActive(householdId);
        using var activity = Telemetry.Source.StartActivity("foodassistant.households.revoke_invitation");

        var outcome = await store
            .RevokeAsync(invitationId, timeProvider.GetUtcNow(), cancellationToken)
            .ConfigureAwait(false);

        activity?.SetTag("foodassistant.outcome", outcome.ToString());
        if (outcome == RevokeInvitationOutcome.Revoked)
        {
            HouseholdsLog.InvitationRevoked(logger, invitationId, householdId);
        }

        return outcome;
    }
}
