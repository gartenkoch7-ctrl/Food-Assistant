using FoodAssistant.Client.Households;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FoodAssistant.Client.Composition;

/// <summary>Registers the Households client services.</summary>
public static class HouseholdsClientRegistration
{
    public static IServiceCollection AddHouseholdsClient(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddScoped<IHouseholdClient, HouseholdClient>();
        return services;
    }
}
