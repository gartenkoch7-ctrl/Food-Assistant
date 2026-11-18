using FoodAssistant.Domain.Inventory;

namespace FoodAssistant.Application.Inventory;

/// <summary>New state of a loaded batch.</summary>
/// <param name="Batch">The changed domain batch.</param>
/// <param name="StorageLocationId">Location after the change.</param>
/// <param name="ExpectedVersion">Version the change is based on; a different stored version is a conflict.</param>
public sealed record BatchUpdate(StockBatch Batch, Guid StorageLocationId, long ExpectedVersion);
