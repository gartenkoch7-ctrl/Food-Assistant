using FoodAssistant.Application.Checkout;
using FoodAssistant.Infrastructure.Checkout;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace FoodAssistant.Infrastructure.Composition;

/// <summary>Persistence of completing a shopping list (track F).</summary>
public static class CheckoutInfrastructureRegistration
{
    public static IHostApplicationBuilder AddCheckoutPersistence(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Services.AddScoped<ICheckoutStore, CheckoutStore>();
        return builder;
    }
}
