using Microsoft.Extensions.DependencyInjection;

namespace FoodAssistant.Client.Composition;

/// <summary>Registers the ShelfLife client services. Empty until the owning track fills it (plan M1, S0.3 hook).</summary>
public static class ShelfLifeClientRegistration
{
    public static IServiceCollection AddShelfLifeClient(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services;
    }
}
