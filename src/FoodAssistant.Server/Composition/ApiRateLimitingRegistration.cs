using System.Threading.RateLimiting;
using FoodAssistant.Server.Api;
using FoodAssistant.Server.Api.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace FoodAssistant.Server.Composition;

/// <summary>Registers the <see cref="RateLimitPolicies"/> (ADR-0003). Part of <see cref="ApiRegistration"/>.</summary>
internal static class ApiRateLimitingRegistration
{
    public static IServiceCollection AddApiRateLimiting(this IServiceCollection services)
    {
        services.AddOptions<ApiRateLimitOptions>()
            .BindConfiguration(ApiRateLimitOptions.SectionName)
            .Validate(
                o => o.AuthStrictPermitLimit > 0 && o.AuthStrictWindowSeconds > 0 && o.ApiPermitLimit > 0 && o.ApiWindowSeconds > 0,
                "FoodAssistant:RateLimits values must be positive.")
            .ValidateOnStart();
        services.AddSingleton<RateLimitRejectionHandler>();

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = (context, cancellationToken) => context.HttpContext.RequestServices
                .GetRequiredService<RateLimitRejectionHandler>().HandleAsync(context, cancellationToken);

            // Policies only apply to matched endpoints; API requests that match none (scanners
            // probing random paths) are limited like the api policy, per client network.
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                ApiRequests.IsApi(context) && context.GetEndpoint() is null
                    ? RateLimitPartition.GetFixedWindowLimiter("unmatched:" + ApiRequests.ClientAddressKey(context), _ => ApiWindow(context))
                    : RateLimitPartition.GetNoLimiter(string.Empty));

            // Sign-in is anonymous: always per client network.
            options.AddPolicy(RateLimitPolicies.AuthStrict, context => RateLimitPartition.GetFixedWindowLimiter(
                ApiRequests.ClientAddressKey(context),
                _ => AuthStrictWindow(context)));

            // Per signed-in person: the limiter runs after authentication (AuthRegistration.UseAuth).
            options.AddPolicy(RateLimitPolicies.Api, context => RateLimitPartition.GetFixedWindowLimiter(
                ApiRequests.CallerKey(context),
                _ => ApiWindow(context)));
        });
        return services;
    }

    /// <summary>
    /// Limits the OpenID endpoints per client network. Must run before authentication: OpenIddict
    /// validates and answers these requests (including rejections and database lookups) inside
    /// <c>UseAuthentication</c>, so the endpoint limiter after it would never see them.
    /// </summary>
    public static IApplicationBuilder UseOpenIdRateLimiting(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        var options = new RateLimiterOptions
        {
            RejectionStatusCode = StatusCodes.Status429TooManyRequests,
            OnRejected = (context, cancellationToken) => context.HttpContext.RequestServices
                .GetRequiredService<RateLimitRejectionHandler>().HandleAsync(context, cancellationToken),
            GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                RateLimitPartition.GetFixedWindowLimiter("openid:" + ApiRequests.ClientAddressKey(context), _ => ApiWindow(context))),
        };
        return app.UseWhen(ApiRequests.IsOpenId, branch => branch.UseRateLimiter(options));
    }

    // Called once per new partition, not per request.
    private static FixedWindowRateLimiterOptions AuthStrictWindow(HttpContext context)
    {
        var limits = context.RequestServices.GetRequiredService<IOptions<ApiRateLimitOptions>>().Value;
        return Window(limits.AuthStrictPermitLimit, limits.AuthStrictWindowSeconds);
    }

    private static FixedWindowRateLimiterOptions ApiWindow(HttpContext context)
    {
        var limits = context.RequestServices.GetRequiredService<IOptions<ApiRateLimitOptions>>().Value;
        return Window(limits.ApiPermitLimit, limits.ApiWindowSeconds);
    }

    private static FixedWindowRateLimiterOptions Window(int permitLimit, int windowSeconds) => new()
    {
        PermitLimit = permitLimit,
        Window = TimeSpan.FromSeconds(windowSeconds),
        QueueLimit = 0,
    };
}
