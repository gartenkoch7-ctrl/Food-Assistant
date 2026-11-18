using Microsoft.Extensions.Hosting;

namespace FoodAssistant.Infrastructure.Composition;

/// <summary>Extension point for track C1 (docs/plan/m01-mvp.md). Empty until the feature is implemented.</summary>
public static class FoodFactsInfrastructureRegistration
{
    public static IHostApplicationBuilder AddFoodFactsPersistence(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder;
    }
}
