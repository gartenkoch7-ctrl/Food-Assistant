using FoodAssistant.Contracts.Households;

namespace FoodAssistant.Application.Households;

/// <summary>Invitation rules of ADR-0002: single use, 7 days valid, revocable.</summary>
public static class InvitationRules
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromDays(7);

    /// <summary>Status of an invitation at <paramref name="now"/>; accepted and revoked win over expired.</summary>
    public static InvitationStatus StatusAt(InvitationInfo invitation, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(invitation);

        if (invitation.AcceptedAt is not null)
        {
            return InvitationStatus.Accepted;
        }

        if (invitation.RevokedAt is not null)
        {
            return InvitationStatus.Revoked;
        }

        return now >= invitation.ExpiresAt ? InvitationStatus.Expired : InvitationStatus.Pending;
    }
}
