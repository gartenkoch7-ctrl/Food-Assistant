using FoodAssistant.Application.Catalog;
using FoodAssistant.Infrastructure.Catalog;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace FoodAssistant.Infrastructure.Composition;

/// <summary>Registers the catalog persistence (products, barcodes, storage locations).</summary>
public static class CatalogInfrastructureRegistration
{
    public static IHostApplicationBuilder AddCatalogPersistence(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Services.AddScoped<ICatalogStore, CatalogStore>();
        return builder;
    }
}
