using FoodAssistant.Domain.Inventory;

namespace FoodAssistant.Application.Inventory;

/// <summary>Where a batch belonged before an action; needed to restore or re-create it on undo.</summary>
public sealed record BatchLocationInfo(Guid ProductId, Guid StorageLocationId, BatchOrigin Origin, DateOnly AddedOn);
