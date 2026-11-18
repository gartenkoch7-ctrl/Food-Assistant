using FoodAssistant.Client.Checkout;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FoodAssistant.Client.Composition;

/// <summary>Registers the checkout client (completing a shopping list).</summary>
public static class CheckoutClientRegistration
{
    public static IServiceCollection AddCheckoutClient(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // TryAdd: the mobile app may register its own variant first (ADR-0005).
        services.TryAddScoped<ICheckoutClient, CheckoutClient>();
        return services;
    }
}
