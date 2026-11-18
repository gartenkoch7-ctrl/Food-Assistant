namespace FoodAssistant.Contracts.Households;

/// <summary>Stable <c>errorCode</c> values of invitation endpoints (ADR-0004).</summary>
public static class InvitationErrorCodes
{
    /// <summary>Token unknown, expired, revoked or already used (404, deliberately not distinguished).</summary>
    public const string InvalidOrExpired = "invitations.invalid_or_expired";

    /// <summary>Invitation id unknown in this household (404).</summary>
    public const string NotFound = "invitations.not_found";

    /// <summary>The invitation was already accepted and can no longer be revoked (409).</summary>
    public const string AlreadyAccepted = "invitations.already_accepted";

    /// <summary>The signed-in person is already a member of the inviting household (409).</summary>
    public const string AlreadyMember = "invitations.already_member";
}
