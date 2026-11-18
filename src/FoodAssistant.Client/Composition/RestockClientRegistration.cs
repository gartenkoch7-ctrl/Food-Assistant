using FoodAssistant.Client.Restock;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FoodAssistant.Client.Composition;

/// <summary>Registers the Restock client (recurring shopping rules, track G).</summary>
public static class RestockClientRegistration
{
    public static IServiceCollection AddRestockClient(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddScoped<IRestockClient, RestockClient>();
        return services;
    }
}
