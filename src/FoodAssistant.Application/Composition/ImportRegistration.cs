using Microsoft.Extensions.DependencyInjection;

namespace FoodAssistant.Application.Composition;

/// <summary>Extension point for track L (docs/plan/m01-mvp.md). Empty until the feature is implemented.</summary>
public static class ImportRegistration
{
    public static IServiceCollection AddImport(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services;
    }
}
