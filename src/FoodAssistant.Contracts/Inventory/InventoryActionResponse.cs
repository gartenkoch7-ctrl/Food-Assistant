namespace FoodAssistant.Contracts.Inventory;

/// <summary>
/// Result of an inventory action: the changed or new batches, the ids of batches that are gone
/// (used up), and the activity whose undo reverts the action
/// (<c>POST …/inventory/activities/{activityId}/undo</c>).
/// </summary>
public sealed record InventoryActionResponse(IReadOnlyList<BatchDto> Batches, IReadOnlyList<Guid> RemovedBatchIds, Guid ActivityId);
