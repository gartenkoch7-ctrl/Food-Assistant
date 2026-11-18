using FoodAssistant.Server.Households;

namespace FoodAssistant.Server.Auth;

/// <summary>Maps all account and household endpoints below <c>/api/v1</c>.</summary>
public static class AccountEndpoints
{
    public static RouteGroupBuilder MapAccountEndpoints(this RouteGroupBuilder api)
    {
        ArgumentNullException.ThrowIfNull(api);
        api.MapSetupEndpoints();
        api.MapLoginEndpoints();
        api.MapMeEndpoints();
        api.MapInvitationAcceptEndpoints();
        api.MapHouseholdEndpoints();
        return api;
    }
}
