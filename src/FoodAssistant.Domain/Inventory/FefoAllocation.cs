using FoodAssistant.Domain.Quantities;

namespace FoodAssistant.Domain.Inventory;

/// <summary>Amount to take from one batch, in the batch's own unit (ready for <see cref="StockBatch.Consume"/>).</summary>
/// <param name="BatchId">Id of the batch.</param>
/// <param name="Quantity">Amount to take from it.</param>
public readonly record struct FefoAllocation(Guid BatchId, Quantity Quantity);
