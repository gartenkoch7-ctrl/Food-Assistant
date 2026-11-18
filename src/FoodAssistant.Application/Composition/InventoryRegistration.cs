using Microsoft.Extensions.DependencyInjection;

namespace FoodAssistant.Application.Composition;

/// <summary>Extension point for track B2 (docs/plan/m01-mvp.md). Empty until the feature is implemented.</summary>
public static class InventoryRegistration
{
    public static IServiceCollection AddInventory(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services;
    }
}
