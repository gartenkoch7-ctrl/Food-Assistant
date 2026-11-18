using FoodAssistant.Application.Inventory;
using Microsoft.Extensions.DependencyInjection;

namespace FoodAssistant.Application.Composition;

/// <summary>Use cases of the inventory (track B2): batches, consumption, undo, overview.</summary>
public static class InventoryRegistration
{
    public static IServiceCollection AddInventory(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddScoped<InventoryCommitter>();
        services.AddScoped<InventoryQueryService>();
        services.AddScoped<BatchAddService>();
        services.AddScoped<BatchService>();
        services.AddScoped<ConsumptionService>();
        services.AddScoped<UndoService>();
        return services;
    }
}
