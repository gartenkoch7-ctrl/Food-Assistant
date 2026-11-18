using Microsoft.Extensions.Hosting;

namespace FoodAssistant.Infrastructure.Composition;

/// <summary>Extension point for track F (docs/plan/m01-mvp.md). Empty until the feature is implemented.</summary>
public static class CheckoutInfrastructureRegistration
{
    public static IHostApplicationBuilder AddCheckoutPersistence(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder;
    }
}
