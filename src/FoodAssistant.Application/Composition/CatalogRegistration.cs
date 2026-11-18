using FoodAssistant.Application.Catalog;
using Microsoft.Extensions.DependencyInjection;

namespace FoodAssistant.Application.Composition;

/// <summary>Registers the catalog use cases (products, barcodes, storage locations).</summary>
public static class CatalogRegistration
{
    public static IServiceCollection AddCatalog(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddScoped<ProductService>();
        services.AddScoped<StorageLocationService>();
        return services;
    }
}
