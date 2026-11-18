using Microsoft.Extensions.Hosting;

namespace FoodAssistant.Infrastructure.Composition;

/// <summary>Extension point for track B1 (docs/plan/m01-mvp.md). Empty until the feature is implemented.</summary>
public static class ShelfLifeInfrastructureRegistration
{
    public static IHostApplicationBuilder AddShelfLifePersistence(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder;
    }
}
