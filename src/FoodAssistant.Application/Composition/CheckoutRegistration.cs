using FoodAssistant.Application.Checkout;
using Microsoft.Extensions.DependencyInjection;

namespace FoodAssistant.Application.Composition;

/// <summary>Use cases of completing a shopping list (track F).</summary>
public static class CheckoutRegistration
{
    public static IServiceCollection AddCheckout(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddScoped<CheckoutPlanner>();
        services.AddScoped<CheckoutService>();
        return services;
    }
}
