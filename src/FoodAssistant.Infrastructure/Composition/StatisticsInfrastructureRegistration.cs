using Microsoft.Extensions.Hosting;

namespace FoodAssistant.Infrastructure.Composition;

/// <summary>Extension point for track I (docs/plan/m01-mvp.md). Empty until the feature is implemented.</summary>
public static class StatisticsInfrastructureRegistration
{
    public static IHostApplicationBuilder AddStatisticsPersistence(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder;
    }
}
