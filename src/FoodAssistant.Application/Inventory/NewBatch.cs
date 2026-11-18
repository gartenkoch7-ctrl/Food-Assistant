using FoodAssistant.Domain.Inventory;

namespace FoodAssistant.Application.Inventory;

/// <summary>A batch to insert (new, or re-created with its old id by undo).</summary>
public sealed record NewBatch(StockBatch Batch, Guid ProductId, Guid StorageLocationId, BatchOrigin Origin, DateOnly AddedOn);
