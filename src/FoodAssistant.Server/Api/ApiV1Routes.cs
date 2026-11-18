using FoodAssistant.Server.Auth;

namespace FoodAssistant.Server.Api;

/// <summary>Root of <c>/api/v1</c>. Each feature maps its own group from here (hotspot, additive only).</summary>
public static class ApiV1Routes
{
    public static RouteGroupBuilder MapApiV1(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        var api = endpoints.MapGroup("/api/v1");
        api.MapAccountEndpoints();
        return api;
    }
}
