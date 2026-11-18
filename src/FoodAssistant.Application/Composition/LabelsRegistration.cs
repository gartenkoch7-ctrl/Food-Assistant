using Microsoft.Extensions.DependencyInjection;

namespace FoodAssistant.Application.Composition;

/// <summary>Extension point for track J (docs/plan/m01-mvp.md). Empty until the feature is implemented.</summary>
public static class LabelsRegistration
{
    public static IServiceCollection AddLabels(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services;
    }
}
