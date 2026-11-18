using System.Text;
using FoodAssistant.Client.Http;
using FoodAssistant.Contracts.Catalog;
using FoodAssistant.Contracts.Common;

namespace FoodAssistant.Client.Catalog;

/// <summary>The catalog API of a household over <see cref="ApiHttpClient"/>; user input is escaped into the route.</summary>
public sealed class CatalogClient(ApiHttpClient api) : ICatalogClient
{
    public Task<ApiResult<CursorPage<ProductSummaryDto>>> SearchProductsAsync(
        Guid householdId, string? query, string? cursor, int? limit, CancellationToken cancellationToken)
    {
        var parameters = new StringBuilder();
        Append(parameters, "query", string.IsNullOrWhiteSpace(query) ? null : query);
        Append(parameters, "cursor", cursor);
        Append(parameters, "limit", limit?.ToString(System.Globalization.CultureInfo.InvariantCulture));
        return api.GetAsync<CursorPage<ProductSummaryDto>>(ApiPaths.Household(householdId, "products" + parameters), cancellationToken);
    }

    public Task<ApiResult<ProductDto>> GetProductAsync(Guid householdId, Guid productId, CancellationToken cancellationToken) =>
        api.GetAsync<ProductDto>(ApiPaths.Household(householdId, $"products/{productId:D}"), cancellationToken);

    public Task<ApiResult<ProductDto>> FindByBarcodeAsync(Guid householdId, string barcode, CancellationToken cancellationToken) =>
        api.GetAsync<ProductDto>(ApiPaths.Household(householdId, $"products/by-barcode/{ApiPaths.Escape(barcode)}"), cancellationToken);

    public Task<ApiResult<ProductDto>> CreateProductAsync(Guid householdId, CreateProductRequest request, CancellationToken cancellationToken) =>
        api.SendAsync<CreateProductRequest, ProductDto>(
            HttpMethod.Post, ApiPaths.Household(householdId, "products"), request, null, null, cancellationToken);

    public Task<ApiResult<ProductDto>> UpdateProductAsync(
        Guid householdId, Guid productId, UpdateProductRequest request, string etag, CancellationToken cancellationToken) =>
        api.SendAsync<UpdateProductRequest, ProductDto>(
            HttpMethod.Put, ApiPaths.Household(householdId, $"products/{productId:D}"), request, etag, null, cancellationToken);

    public Task<ApiResult> ArchiveProductAsync(Guid householdId, Guid productId, string etag, CancellationToken cancellationToken) =>
        api.SendAsync<object>(
            HttpMethod.Delete, ApiPaths.Household(householdId, $"products/{productId:D}"), null, etag, null, cancellationToken);

    public Task<ApiResult<IReadOnlyList<StorageLocationDto>>> ListStorageLocationsAsync(Guid householdId, CancellationToken cancellationToken) =>
        api.GetAsync<IReadOnlyList<StorageLocationDto>>(ApiPaths.Household(householdId, "storage-locations"), cancellationToken);

    public Task<ApiResult<StorageLocationDto>> CreateStorageLocationAsync(
        Guid householdId, StorageLocationRequest request, CancellationToken cancellationToken) =>
        api.SendAsync<StorageLocationRequest, StorageLocationDto>(
            HttpMethod.Post, ApiPaths.Household(householdId, "storage-locations"), request, null, null, cancellationToken);

    public Task<ApiResult<StorageLocationDto>> UpdateStorageLocationAsync(
        Guid householdId, Guid locationId, StorageLocationRequest request, string etag, CancellationToken cancellationToken) =>
        api.SendAsync<StorageLocationRequest, StorageLocationDto>(
            HttpMethod.Put, ApiPaths.Household(householdId, $"storage-locations/{locationId:D}"), request, etag, null, cancellationToken);

    public Task<ApiResult> DeleteStorageLocationAsync(Guid householdId, Guid locationId, string etag, CancellationToken cancellationToken) =>
        api.SendAsync<object>(
            HttpMethod.Delete, ApiPaths.Household(householdId, $"storage-locations/{locationId:D}"), null, etag, null, cancellationToken);

    public Task<ApiResult<IReadOnlyList<CategoryDto>>> ListCategoriesAsync(Guid householdId, CancellationToken cancellationToken) =>
        api.GetAsync<IReadOnlyList<CategoryDto>>(ApiPaths.Household(householdId, "catalog/categories"), cancellationToken);

    private static void Append(StringBuilder parameters, string name, string? value)
    {
        if (value is null)
        {
            return;
        }

        parameters.Append(parameters.Length == 0 ? '?' : '&').Append(name).Append('=').Append(ApiPaths.Escape(value));
    }
}
