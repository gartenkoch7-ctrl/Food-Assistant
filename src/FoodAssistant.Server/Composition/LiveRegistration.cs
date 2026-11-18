using FoodAssistant.Application.Households;
using FoodAssistant.Application.Sync;
using FoodAssistant.Contracts.Sync;
using FoodAssistant.Server.Api.RateLimiting;
using FoodAssistant.Server.Households;
using FoodAssistant.Server.Sync;

namespace FoodAssistant.Server.Composition;

/// <summary>Live updates via SignalR (ADR-0005) and household scopes for background services.</summary>
public static class LiveRegistration
{
    public static WebApplicationBuilder AddLive(this WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        var services = builder.Services;

        services.AddSignalR();

        // Replaces the NullHouseholdChangeNotifier default of the application layer (last registration wins).
        services.AddSingleton<IHouseholdChangeNotifier, SignalRHouseholdChangeNotifier>();

        // Singleton (it only needs IServiceScopeFactory), so hosted services can depend on it.
        services.AddSingleton<IHouseholdScopeFactory, HouseholdScopeFactory>();
        return builder;
    }

    public static WebApplication MapLive(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);
        app.MapHub<HouseholdHub>(HouseholdHubContract.Path).RequireRateLimiting(RateLimitPolicies.Api);
        return app;
    }
}
