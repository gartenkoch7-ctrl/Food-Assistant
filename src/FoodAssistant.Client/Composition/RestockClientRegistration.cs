using Microsoft.Extensions.DependencyInjection;

namespace FoodAssistant.Client.Composition;

/// <summary>Registers the Restock client services. Empty until the owning track fills it (plan M1, S0.3 hook).</summary>
public static class RestockClientRegistration
{
    public static IServiceCollection AddRestockClient(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services;
    }
}
