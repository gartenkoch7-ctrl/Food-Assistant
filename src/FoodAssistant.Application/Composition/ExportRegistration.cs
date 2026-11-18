using Microsoft.Extensions.DependencyInjection;

namespace FoodAssistant.Application.Composition;

/// <summary>Extension point for track N (docs/plan/m01-mvp.md). Empty until the feature is implemented.</summary>
public static class ExportRegistration
{
    public static IServiceCollection AddExport(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services;
    }
}
