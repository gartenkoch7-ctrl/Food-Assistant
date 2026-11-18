using Microsoft.Extensions.Hosting;

namespace FoodAssistant.Infrastructure.Composition;

/// <summary>Extension point for track A (docs/plan/m01-mvp.md). Empty until the feature is implemented.</summary>
public static class CatalogInfrastructureRegistration
{
    public static IHostApplicationBuilder AddCatalogPersistence(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder;
    }
}
