using Microsoft.Extensions.DependencyInjection;

namespace FoodAssistant.Application.Composition;

/// <summary>Extension point for track V (docs/plan/m01-mvp.md). Empty until the feature is implemented.</summary>
public static class ActivityRegistration
{
    public static IServiceCollection AddActivity(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services;
    }
}
