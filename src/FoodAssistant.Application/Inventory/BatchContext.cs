using FoodAssistant.Domain.Inventory;

namespace FoodAssistant.Application.Inventory;

/// <summary>A stored batch: the domain batch plus where it belongs; never an EF entity.</summary>
/// <param name="Batch">Domain state; changes go back through <see cref="IInventoryStore.SaveAsync"/>.</param>
/// <param name="ProductId">Product of the batch.</param>
/// <param name="StorageLocationId">Current storage location.</param>
/// <param name="Origin">How the batch came into the inventory.</param>
/// <param name="AddedOn">Household date the batch was added.</param>
/// <param name="UpdatedAt">Time of the last change; undo compares it (see <see cref="BatchUndoEntry"/>).</param>
/// <param name="Version">Concurrency version (ETag).</param>
public sealed record BatchContext(
    StockBatch Batch,
    Guid ProductId,
    Guid StorageLocationId,
    BatchOrigin Origin,
    DateOnly AddedOn,
    DateTimeOffset UpdatedAt,
    long Version);
