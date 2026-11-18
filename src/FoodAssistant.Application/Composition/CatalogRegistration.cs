using Microsoft.Extensions.DependencyInjection;

namespace FoodAssistant.Application.Composition;

/// <summary>Extension point for track A (docs/plan/m01-mvp.md). Empty until the feature is implemented.</summary>
public static class CatalogRegistration
{
    public static IServiceCollection AddCatalog(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services;
    }
}
