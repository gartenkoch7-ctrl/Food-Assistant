using FoodAssistant.Client.Http;
using FoodAssistant.Contracts.Catalog;
using FoodAssistant.Contracts.Common;

namespace FoodAssistant.Client.Catalog;

/// <summary>
/// Products, storage locations and categories of one household. Contract of S0; the Catalog track
/// implements it. The UI only talks to this interface so the mobile app can swap in a cached variant (ADR-0005).
/// </summary>
public interface ICatalogClient
{
    Task<ApiResult<CursorPage<ProductSummaryDto>>> SearchProductsAsync(
        Guid householdId, string? query, string? cursor, int? limit, CancellationToken cancellationToken);

    Task<ApiResult<ProductDto>> GetProductAsync(Guid householdId, Guid productId, CancellationToken cancellationToken);

    Task<ApiResult<ProductDto>> FindByBarcodeAsync(Guid householdId, string barcode, CancellationToken cancellationToken);

    Task<ApiResult<ProductDto>> CreateProductAsync(Guid householdId, CreateProductRequest request, CancellationToken cancellationToken);

    Task<ApiResult<ProductDto>> UpdateProductAsync(
        Guid householdId, Guid productId, UpdateProductRequest request, string etag, CancellationToken cancellationToken);

    Task<ApiResult> ArchiveProductAsync(Guid householdId, Guid productId, string etag, CancellationToken cancellationToken);

    Task<ApiResult<IReadOnlyList<StorageLocationDto>>> ListStorageLocationsAsync(Guid householdId, CancellationToken cancellationToken);

    Task<ApiResult<StorageLocationDto>> CreateStorageLocationAsync(
        Guid householdId, StorageLocationRequest request, CancellationToken cancellationToken);

    Task<ApiResult<StorageLocationDto>> UpdateStorageLocationAsync(
        Guid householdId, Guid locationId, StorageLocationRequest request, string etag, CancellationToken cancellationToken);

    Task<ApiResult> DeleteStorageLocationAsync(Guid householdId, Guid locationId, string etag, CancellationToken cancellationToken);

    Task<ApiResult<IReadOnlyList<CategoryDto>>> ListCategoriesAsync(Guid householdId, CancellationToken cancellationToken);
}
