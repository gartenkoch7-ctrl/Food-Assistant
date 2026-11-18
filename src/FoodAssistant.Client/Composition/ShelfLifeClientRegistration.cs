using FoodAssistant.Client.ShelfLife;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FoodAssistant.Client.Composition;

/// <summary>Registers the ShelfLife client services (track B1).</summary>
public static class ShelfLifeClientRegistration
{
    public static IServiceCollection AddShelfLifeClient(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // TryAdd: a host (the mobile app) may register a cached variant first.
        services.TryAddScoped<IShelfLifeClient, ShelfLifeClient>();
        return services;
    }
}
