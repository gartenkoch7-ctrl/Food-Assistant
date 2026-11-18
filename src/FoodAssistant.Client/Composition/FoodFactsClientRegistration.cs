using FoodAssistant.Client.FoodFacts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FoodAssistant.Client.Composition;

/// <summary>Registers the Open Food Facts lookup client.</summary>
public static class FoodFactsClientRegistration
{
    public static IServiceCollection AddFoodFactsClient(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddScoped<IFoodFactsClient, FoodFactsClient>();
        return services;
    }
}
