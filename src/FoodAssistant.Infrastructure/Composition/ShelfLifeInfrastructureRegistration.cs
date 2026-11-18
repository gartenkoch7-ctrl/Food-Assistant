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
        // Loaded here, not lazily: a broken shipped table stops the start instead of failing requests later.
        builder.Services.AddSingleton<IDefaultShelfLifeRules>(EmbeddedDefaultShelfLifeRules.Load());
        builder.Services.AddScoped<IShelfLifeOverrideStore, ShelfLifeOverrideStore>();
        return builder;
    }
}
