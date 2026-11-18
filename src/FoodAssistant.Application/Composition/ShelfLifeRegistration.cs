using FoodAssistant.Application.ShelfLife;
using Microsoft.Extensions.DependencyInjection;

namespace FoodAssistant.Application.Composition;

/// <summary>Shelf-life rules (track B1, docs/plan/m01-mvp.md).</summary>
public static class ShelfLifeRegistration
{
    public static IServiceCollection AddShelfLife(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Plain AddScoped, not TryAdd: replaces the NoShelfLifeRules placeholder of step 0.
        services.AddScoped<IShelfLifeRules, ShelfLifeRuleService>();
        services.AddScoped<ShelfLifeAdminService>();
        services.AddScoped<ProductShelfLifeService>();
        return services;
    }
}
