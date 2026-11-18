using FoodAssistant.Client.Shopping;
using FoodAssistant.Client.Stores;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FoodAssistant.Client.Composition;

/// <summary>Registers the Shopping and Stores clients (track D3).</summary>
public static class ShoppingClientRegistration
{
    public static IServiceCollection AddShoppingClient(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // TryAdd: a host (the mobile app) may register an offline-capable variant first.
        services.TryAddScoped<IShoppingClient, ShoppingClient>();
        services.TryAddScoped<IStoreClient, StoreClient>();
        return services;
    }
}
