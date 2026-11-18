using FoodAssistant.Application.ShelfLife;
using FoodAssistant.Infrastructure.ShelfLife;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace FoodAssistant.Infrastructure.Composition;

/// <summary>Shelf-life defaults and household overrides (track B1, docs/plan/m01-mvp.md).</summary>
public static class ShelfLifeInfrastructureRegistration
{
    public static IHostApplicationBuilder AddShelfLifePersistence(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Services.AddSingleton<IDefaultShelfLifeRules>(_ => EmbeddedDefaultShelfLifeRules.Load());
        builder.Services.AddScoped<IShelfLifeOverrideStore, ShelfLifeOverrideStore>();
        return builder;
    }
}
