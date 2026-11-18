using FoodAssistant.Contracts.Inventory;

namespace FoodAssistant.Contracts.Catalog;

/// <summary>A storage location of the household (fridge, freezer, pantry …).</summary>
public sealed record StorageLocationDto(Guid Id, string Name, StorageTypeDto Type, int SortOrder, long Version);
