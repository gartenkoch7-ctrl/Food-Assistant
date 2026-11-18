namespace FoodAssistant.Contracts.Households;

/// <summary>Stable <c>errorCode</c> values of household endpoints (ADR-0004). The UI translates these.</summary>
public static class HouseholdErrorCodes
{
    /// <summary>Unknown household or no membership (404, no hint about existence).</summary>
    public const string NotFound = "households.not_found";

    /// <summary>Member of the household, but the role does not allow the action (403).</summary>
    public const string Forbidden = "households.forbidden";

    /// <summary>The change would leave the household without an owner (409).</summary>
    public const string LastOwner = "households.last_owner";

    /// <summary>The person is not a member of this household (404).</summary>
    public const string MemberNotFound = "households.member_not_found";

    /// <summary>Request data is invalid; see <c>errors</c> (400).</summary>
    public const string ValidationFailed = "households.validation_failed";
}
