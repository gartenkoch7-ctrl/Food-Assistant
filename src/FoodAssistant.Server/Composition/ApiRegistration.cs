using FoodAssistant.Server.Api.Hosting;
using FoodAssistant.Server.Api.Info;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FoodAssistant.Server.Composition;

/// <summary>API conventions of ADR-0004: errors, validation, idempotency, rate limits, OpenAPI. Owned by the API/telemetry track.</summary>
public static class ApiRegistration
{
    public static WebApplicationBuilder AddApi(this WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        var services = builder.Services;

        services.TryAddSingleton(TimeProvider.System);
        services.AddApiHosting();
        services.AddApiErrors();
        services.AddValidation();
        services.AddApiIdempotency();
        services.AddApiRateLimiting();
        services.AddApiDocument();
        services.AddOptions<ServerInfoOptions>()
            .BindConfiguration(HostingOptions.SectionName)
            .Validate(o => o.HasValidMinimumAppVersion(), "FoodAssistant:MinimumAppVersion must be major.minor.patch, e.g. 1.4.0.")
            .ValidateOnStart();
        return builder;
    }

    /// <summary>
    /// Runs before <c>UseAuth</c> in Program.cs. Error handling must wrap everything after it,
    /// including 401/403 from the auth middleware. The rate limiter also sees requests that
    /// authentication rejects later (token guessing), but for the same reason it only knows the
    /// person if authentication already ran before it; otherwise the <c>api</c> policy limits per
    /// client network. Body buffering only wraps the stream; nothing is read here.
    /// </summary>
    public static WebApplication UseApi(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);
        app.UseApiErrors();
        app.UseRateLimiter();
        app.UseIdempotencyBuffering();
        return app;
    }
}
