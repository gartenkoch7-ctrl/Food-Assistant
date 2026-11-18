using Microsoft.Extensions.DependencyInjection;

namespace FoodAssistant.Client.Composition;

/// <summary>Registers the Activity client services. Empty until the owning track fills it (plan M1, S0.3 hook).</summary>
public static class ActivityClientRegistration
{
    public static IServiceCollection AddActivityClient(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services;
    }
}
