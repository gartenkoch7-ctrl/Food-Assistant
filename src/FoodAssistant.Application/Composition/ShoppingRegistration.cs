using Microsoft.Extensions.DependencyInjection;

namespace FoodAssistant.Application.Composition;

/// <summary>Extension point for track D (docs/plan/m01-mvp.md). Empty until the feature is implemented.</summary>
public static class ShoppingRegistration
{
    public static IServiceCollection AddShopping(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services;
    }
}
