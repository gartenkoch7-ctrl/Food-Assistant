using FoodAssistant.Application.Inventory;
using FoodAssistant.Infrastructure.Inventory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace FoodAssistant.Infrastructure.Composition;

/// <summary>Persistence of the inventory (track B2).</summary>
public static class InventoryInfrastructureRegistration
{
    public static IHostApplicationBuilder AddInventoryPersistence(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Services.AddScoped<IInventoryStore, InventoryStore>();
        return builder;
    }
}
