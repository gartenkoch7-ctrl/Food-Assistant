namespace FoodAssistant.Server.Auth;

/// <summary>
/// Authorization policies (ADR-0002). Endpoints require these instead of comparing roles.
/// Household policies read the household from the route value <c>householdId</c>.
/// </summary>
public static class AuthPolicies
{
    /// <summary>The account is the server admin.</summary>
    public const string ServerAdmin = "ServerAdmin";

    /// <summary>Owner of the household in the route.</summary>
    public const string HouseholdOwner = "HouseholdOwner";

    /// <summary>Member or owner of the household in the route.</summary>
    public const string HouseholdMember = "HouseholdMember";

    /// <summary>Any role in the household in the route, including restricted.</summary>
    public const string HouseholdAny = "HouseholdAny";

    /// <summary>Route value that household policies and <c>HttpHouseholdContext</c> read.</summary>
    public const string HouseholdRouteValue = "householdId";
}
