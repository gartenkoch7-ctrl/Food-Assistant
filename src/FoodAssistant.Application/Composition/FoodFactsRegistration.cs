using Microsoft.Extensions.DependencyInjection;

namespace FoodAssistant.Application.Composition;

/// <summary>Extension point for track C1 (docs/plan/m01-mvp.md). Empty until the feature is implemented.</summary>
public static class FoodFactsRegistration
{
    public static IServiceCollection AddFoodFacts(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services;
    }
}
