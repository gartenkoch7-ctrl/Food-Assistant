using FoodAssistant.Application.FoodFacts;
using FoodAssistant.Infrastructure.FoodFacts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace FoodAssistant.Infrastructure.Composition;

public static class FoodFactsInfrastructureRegistration
{
    public static IHostApplicationBuilder AddFoodFactsPersistence(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        var services = builder.Services;

        services.AddOptions<OpenFoodFactsOptions>()
            .BindConfiguration(OpenFoodFactsOptions.SectionName)
            .Validate(o => o.Validate() is null, "FoodAssistant:OpenFoodFacts is invalid.")
            .ValidateOnStart();

        // One throttle per server, shared by all requests.
        services.AddSingleton<OpenFoodFactsThrottle>();
        services.AddHttpClient<IFoodFactsApi, OpenFoodFactsApi>();
        services.AddScoped<IFoodFactsCache, FoodFactsCacheStore>();
        services.AddSingleton<IFoodFactsCategoryResolver, FoodFactsCategoryResolver>();
        return builder;
    }
}
