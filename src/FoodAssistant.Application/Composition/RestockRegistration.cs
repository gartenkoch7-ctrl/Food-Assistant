using Microsoft.Extensions.DependencyInjection;

namespace FoodAssistant.Application.Composition;

/// <summary>Extension point for track G (docs/plan/m01-mvp.md). Empty until the feature is implemented.</summary>
public static class RestockRegistration
{
    public static IServiceCollection AddRestock(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services;
    }
}
