using FoodAssistant.Contracts.Catalog;

namespace FoodAssistant.Application.Inventory;

/// <summary>
/// Batches, consumption events and inventory activities of the active household. One instance
/// per request: batches and activities loaded through it are the ones <see cref="SaveAsync"/> writes,
/// and their loaded version is the concurrency check (store pattern, docs/kb/datenmodell.md).
/// </summary>
public interface IInventoryStore
{
    /// <summary>All products that have batches, each with its batches (read only).</summary>
    Task<IReadOnlyList<ProductBatches>> ListAsync(CancellationToken cancellationToken);

    /// <summary>A batch, or null if it does not exist in the active household.</summary>
    Task<BatchContext?> GetBatchAsync(Guid batchId, CancellationToken cancellationToken);

    /// <summary>All batches of a product.</summary>
    Task<IReadOnlyList<BatchContext>> GetBatchesOfProductAsync(Guid productId, CancellationToken cancellationToken);

    /// <summary>A product that is not archived, or null.</summary>
    Task<ProductSummaryDto?> GetProductAsync(Guid productId, CancellationToken cancellationToken);

    /// <summary>A storage location, or null.</summary>
    Task<StorageLocationInfo?> GetLocationAsync(Guid locationId, CancellationToken cancellationToken);

    /// <summary>An inventory activity, or null if it does not exist or belongs to another area.</summary>
    Task<InventoryActivity?> GetActivityAsync(Guid activityId, CancellationToken cancellationToken);

    /// <summary>
    /// Stores the change together with activity entries added through <c>IActivityLog</c> in this
    /// request, in one transaction. On anything but <see cref="BatchSaveStatus.Saved"/> nothing is
    /// stored and the pending entries are discarded.
    /// </summary>
    Task<BatchSaveResult> SaveAsync(BatchChange change, CancellationToken cancellationToken);
}
