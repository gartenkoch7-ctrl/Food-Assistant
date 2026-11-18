namespace FoodAssistant.Application.Households;

/// <summary>Result of revoking an invitation.</summary>
public enum RevokeInvitationOutcome
{
    Revoked = 0,
    NotFound = 1,
    AlreadyAccepted = 2,
}
