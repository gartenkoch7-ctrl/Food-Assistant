using Microsoft.Extensions.Hosting;

namespace FoodAssistant.Infrastructure.Composition;

/// <summary>Extension point for track B2 (docs/plan/m01-mvp.md). Empty until the feature is implemented.</summary>
public static class InventoryInfrastructureRegistration
{
    public static IHostApplicationBuilder AddInventoryPersistence(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder;
    }
}
