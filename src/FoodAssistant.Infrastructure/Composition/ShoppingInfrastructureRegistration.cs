using FoodAssistant.Application.Shopping;
using FoodAssistant.Infrastructure.Shopping;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace FoodAssistant.Infrastructure.Composition;

/// <summary>Persistence of shopping lists, entries and stores (M1 track D).</summary>
public static class ShoppingInfrastructureRegistration
{
    public static IHostApplicationBuilder AddShoppingPersistence(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Services.AddScoped<IShoppingListStore, ShoppingListStore>();
        builder.Services.AddScoped<IShoppingItemStore, ShoppingItemStore>();
        return builder;
    }
}
