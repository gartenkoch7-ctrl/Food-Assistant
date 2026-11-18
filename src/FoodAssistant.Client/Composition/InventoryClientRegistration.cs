using FoodAssistant.Client.Inventory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FoodAssistant.Client.Composition;

/// <summary>Registers the inventory client (overview, batches, consumption, undo).</summary>
public static class InventoryClientRegistration
{
    public static IServiceCollection AddInventoryClient(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // TryAdd: the mobile app registers its cached variant first (ADR-0005).
        services.TryAddScoped<IInventoryClient, InventoryClient>();
        return services;
    }
}
