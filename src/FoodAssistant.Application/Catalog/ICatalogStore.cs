using FoodAssistant.Application.Common;
using FoodAssistant.Contracts.Catalog;

namespace FoodAssistant.Application.Catalog;

/// <summary>
/// Persistence of products, barcodes and storage locations of the active household (Infrastructure
/// implements it). Updates follow the store pattern of docs/kb/datenmodell.md: a stale
/// <c>expectedVersion</c> yields <c>VersionMismatch</c>; <see langword="null"/> skips the check (<c>If-Match: *</c>).
/// </summary>
public interface ICatalogStore
{
    /// <summary>Not archived products whose name contains <paramref name="query"/> or whose barcode equals it, by name and id.</summary>
    Task<IReadOnlyList<ProductSummaryDto>> SearchAsync(string? query, ProductCursor? after, int take, CancellationToken cancellationToken);

    Task<ProductDto?> GetProductAsync(Guid id, CancellationToken cancellationToken);

    Task<ProductDto?> FindByBarcodeAsync(string code, CancellationToken cancellationToken);

    Task<OperationResult<ProductDto>> AddProductAsync(NewProduct product, CancellationToken cancellationToken);

    Task<OperationResult<ProductDto>> UpdateProductAsync(
        Guid id, long? expectedVersion, ProductChanges changes, CancellationToken cancellationToken);

    /// <summary>Archives the product; fails while batches exist. Frees its barcodes.</summary>
    Task<OperationResult<ProductDto>> ArchiveProductAsync(
        Guid id, long? expectedVersion, DateTimeOffset now, CancellationToken cancellationToken);

    Task<IReadOnlyList<StorageLocationDto>> ListLocationsAsync(CancellationToken cancellationToken);

    Task<OperationResult<StorageLocationDto>> AddLocationAsync(NewStorageLocation location, CancellationToken cancellationToken);

    Task<OperationResult<StorageLocationDto>> UpdateLocationAsync(
        Guid id, long? expectedVersion, StorageLocationChanges changes, CancellationToken cancellationToken);

    /// <summary>Deletes the location; fails while batches exist; products lose it as default location.</summary>
    Task<OperationResult<StorageLocationDto>> DeleteLocationAsync(
        Guid id, long? expectedVersion, DateTimeOffset now, CancellationToken cancellationToken);
}
