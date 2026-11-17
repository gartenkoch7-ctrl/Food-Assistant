namespace FoodAssistant.Server.Api;

/// <summary>Root of <c>/api/v1</c>. Each feature maps its own group from here (hotspot, additive only).</summary>
public static class ApiV1Routes
{
    public static RouteGroupBuilder MapApiV1(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        return endpoints.MapGroup("/api/v1");
    }
}
