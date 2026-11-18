using FoodAssistant.Application.Inventory;
using FoodAssistant.Application.Restock;
using Microsoft.Extensions.DependencyInjection;

namespace FoodAssistant.Application.Composition;

/// <summary>Minimum stock and recurring shopping entries (M1 track G).</summary>
public static class RestockRegistration
{
    public static IServiceCollection AddRestock(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddScoped<IStockChangedHandler, MinimumStockHandler>();
        services.AddScoped<RecurringRuleService>();
        services.AddScoped<IRecurringItemsRunner, RecurringItemsRunner>();
        services.AddHostedService<RecurringItemsJob>();
        return services;
    }
}
