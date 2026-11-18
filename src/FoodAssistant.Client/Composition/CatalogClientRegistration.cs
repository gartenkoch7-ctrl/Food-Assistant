using FoodAssistant.Client.Catalog;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FoodAssistant.Client.Composition;

/// <summary>Registers the catalog client (products, barcodes, storage locations, categories).</summary>
public static class CatalogClientRegistration
{
    public static IServiceCollection AddCatalogClient(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // TryAdd: the mobile app registers its cached variant first (ADR-0005).
        services.TryAddScoped<ICatalogClient, CatalogClient>();
        return services;
    }
}
