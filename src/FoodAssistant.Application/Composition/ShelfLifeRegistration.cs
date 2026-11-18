using Microsoft.Extensions.DependencyInjection;

namespace FoodAssistant.Application.Composition;

/// <summary>Extension point for track B1 (docs/plan/m01-mvp.md). Empty until the feature is implemented.</summary>
public static class ShelfLifeRegistration
{
    public static IServiceCollection AddShelfLife(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services;
    }
}
