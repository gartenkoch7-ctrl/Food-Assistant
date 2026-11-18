using FoodAssistant.Server.Api.Idempotency;
using FoodAssistant.Server.Api.Info;
using FoodAssistant.Server.Api.RateLimiting;
using FoodAssistant.Server.Auth;

namespace FoodAssistant.Server.Api;

/// <summary>Root of <c>/api/v1</c>. Each feature maps its own group from here (hotspot, additive only).</summary>
public static class ApiV1Routes
{
    public static RouteGroupBuilder MapApiV1(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        // Public like the API itself (open source), but rate limited: it is generated per request.
        endpoints.MapOpenApi().RequireRateLimiting(RateLimitPolicies.Api);

        var group = endpoints.MapGroup("/api/v1")
            .RequireRateLimiting(RateLimitPolicies.Api)
            .WithIdempotency();
        group.MapInfoEndpoints();
        group.MapAccountEndpoints();
        return group;
    }
}
