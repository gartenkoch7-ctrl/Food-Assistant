using Microsoft.Extensions.DependencyInjection;

namespace FoodAssistant.Application.Composition;

/// <summary>Extension point for track F (docs/plan/m01-mvp.md). Empty until the feature is implemented.</summary>
public static class CheckoutRegistration
{
    public static IServiceCollection AddCheckout(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services;
    }
}
