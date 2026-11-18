using FoodAssistant.Application.Shopping;
using FoodAssistant.Application.Stores;
using FoodAssistant.Infrastructure.Shopping;
using FoodAssistant.Infrastructure.Stores;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace FoodAssistant.Infrastructure.Composition;

/// <summary>Persistence of shopping lists, entries and stores (M1 track D).</summary>
public static class ShoppingInfrastructureRegistration
{
    public static IHostApplicationBuilder AddShoppingPersistence(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Services.AddScoped<ShoppingListReader>();
        builder.Services.AddScoped<ShoppingListRemover>();
        builder.Services.AddScoped<IShoppingListStore, ShoppingListStore>();
        builder.Services.AddScoped<IShoppingItemStore, ShoppingItemStore>();
        builder.Services.AddScoped<IStoreStore, StoreStore>();
        builder.Services.AddScoped<IStoreSectionOrderStore, StoreSectionOrderStore>();
        return builder;
    }
}
