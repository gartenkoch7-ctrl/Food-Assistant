using FoodAssistant.Application.FoodFacts;
using Microsoft.Extensions.DependencyInjection;

namespace FoodAssistant.Application.Composition;

public static class FoodFactsRegistration
{
    public static IServiceCollection AddFoodFacts(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddScoped<FoodFactsService>();
        return services;
    }
}
