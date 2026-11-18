using FoodAssistant.Client.Accounts;
using FoodAssistant.Client.Http;
using FoodAssistant.Client.Sync;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace FoodAssistant.Client.Composition;

/// <summary>Service registration of the API client, shared by the web host, the server's DI check and later the MAUI app.</summary>
public static class ClientRegistration
{
    /// <param name="services">The service collection.</param>
    /// <param name="baseAddress">Origin of the server; the web app uses its own base address, so cookies flow along.</param>
    /// <param name="enableLive">Connects live updates outside the browser too (the MAUI app).</param>
    public static IServiceCollection AddFoodAssistantClient(this IServiceCollection services, Uri baseAddress, bool enableLive = false)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(baseAddress);

        services.AddTransient<AntiforgeryHandler>();
        services.AddTransient<TraceContextHandler>();
        services.TryAddScoped<SessionExpiry>();

        // Order matters: the trace header is set first, then the antiforgery handler may send its own token request.
        services.AddHttpClient<ApiHttpClient>(client => client.BaseAddress = baseAddress)
            .AddHttpMessageHandler<TraceContextHandler>()
            .AddHttpMessageHandler<AntiforgeryHandler>();

        services.TryAddScoped<IAccountClient, AccountClient>();

        // Live updates run in the browser and, when the host asks for it (MAUI), in the app. On the
        // server the placeholder stays: prerendering has no household hub to listen to.
        if (OperatingSystem.IsBrowser() || enableLive)
        {
            services.TryAddScoped<ILiveUpdates>(provider =>
                new SignalRLiveUpdates(baseAddress, provider.GetRequiredService<ILogger<SignalRLiveUpdates>>()));
        }
        else
        {
            services.TryAddScoped<ILiveUpdates, NoLiveUpdates>();
        }

        return services
            .AddCatalogClient()
            .AddShelfLifeClient()
            .AddInventoryClient()
            .AddFoodFactsClient()
            .AddShoppingClient()
            .AddCheckoutClient()
            .AddRestockClient()
            .AddHouseholdsClient()
            .AddNotificationsClient()
            .AddStatisticsClient()
            .AddLabelsClient()
            .AddImportClient()
            .AddExportClient()
            .AddActivityClient();
    }
}
