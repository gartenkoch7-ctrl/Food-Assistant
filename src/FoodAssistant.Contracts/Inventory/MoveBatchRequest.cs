namespace FoodAssistant.Contracts.Inventory;

/// <summary>Moves a batch to another storage location; a freezer freezes it, leaving the freezer thaws it.</summary>
public sealed record MoveBatchRequest(Guid StorageLocationId);
