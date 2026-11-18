using FoodAssistant.Application.Shopping;
using FoodAssistant.Application.Stores;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FoodAssistant.Application.Composition;

/// <summary>Shopping lists, entries and stores (M1 track D).</summary>
public static class ShoppingRegistration
{
    public static IServiceCollection AddShopping(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddScoped<ShoppingListService>();
        services.AddScoped<ShoppingItemService>();
        services.AddScoped<ShoppingItemChangeService>();
        services.TryAddScoped<ISectionOrderLearning, NoSectionOrderLearning>();
        return services;
    }
}
