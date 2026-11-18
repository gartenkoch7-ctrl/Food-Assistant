using FoodAssistant.Server.Api.Idempotency;
using FoodAssistant.Server.Api.Info;
using FoodAssistant.Server.Api.RateLimiting;

namespace FoodAssistant.Server.Api;

/// <summary>Root of <c>/api/v1</c>. Each feature maps its own group from here (hotspot, additive only).</summary>
public static class ApiV1Routes
{
    /// <summary>
    /// Maps <c>/api/v1</c> with the shared conventions: <see cref="RateLimitPolicies.Api"/> and
    /// <c>Idempotency-Key</c> handling apply to every endpoint of the group, including nested
    /// feature groups. Called once by Program.cs; features add their <c>group.MapXxx()</c> line
    /// below instead of calling this again (that would map info and OpenAPI twice).
    /// </summary>
    public static RouteGroupBuilder MapApiV1(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        // Public like the API itself (open source), but rate limited: it is generated per request.
        endpoints.MapOpenApi().RequireRateLimiting(RateLimitPolicies.Api);

        var group = endpoints.MapGroup("/api/v1")
            .RequireRateLimiting(RateLimitPolicies.Api)
            .WithIdempotency();
        group.MapInfoEndpoints();
        return group;
    }
}
