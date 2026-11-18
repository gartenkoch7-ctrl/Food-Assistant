using FoodAssistant.Domain.Inventory;

namespace FoodAssistant.Application.Inventory;

/// <summary>One batch touched by an inventory action; an emptied loaded batch is deleted.</summary>
/// <param name="Original">The loaded batch, or null for a new one.</param>
/// <param name="Before">State before the action, or null for a new batch.</param>
/// <param name="Batch">State after the action.</param>
/// <param name="ProductId">Product of the batch.</param>
/// <param name="StorageLocationId">Location after the action.</param>
/// <param name="Origin">Origin of the batch.</param>
/// <param name="AddedOn">Household date the batch was added.</param>
/// <param name="Event">Consumption event of the action, if any.</param>
public sealed record BatchStep(
    BatchContext? Original,
    StockBatchSnapshot? Before,
    StockBatch Batch,
    Guid ProductId,
    Guid StorageLocationId,
    BatchOrigin Origin,
    DateOnly AddedOn,
    NewConsumptionEvent? Event)
{
    /// <summary>True if the action used up a stored batch, so it is deleted.</summary>
    public bool Removes => Original is not null && Batch.IsEmpty;

    /// <summary>A loaded batch changed in place; <paramref name="before"/> holds its state before the change.</summary>
    public static BatchStep Changed(
        BatchContext original,
        StockBatchSnapshot before,
        Guid? newLocationId = null,
        NewConsumptionEvent? consumption = null)
    {
        ArgumentNullException.ThrowIfNull(original);
        return new BatchStep(
            original,
            before,
            original.Batch,
            original.ProductId,
            newLocationId ?? original.StorageLocationId,
            original.Origin,
            original.AddedOn,
            consumption);
    }

    /// <summary>A new batch.</summary>
    public static BatchStep Created(StockBatch batch, Guid productId, Guid locationId, BatchOrigin origin, DateOnly addedOn) =>
        new(null, null, batch, productId, locationId, origin, addedOn, null);
}
