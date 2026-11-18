using Microsoft.Extensions.DependencyInjection;

namespace FoodAssistant.Application.Composition;

/// <summary>Extension point for track I (docs/plan/m01-mvp.md). Empty until the feature is implemented.</summary>
public static class StatisticsRegistration
{
    public static IServiceCollection AddStatistics(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services;
    }
}
