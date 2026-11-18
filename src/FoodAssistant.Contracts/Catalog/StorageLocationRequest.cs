using FoodAssistant.Contracts.Inventory;

namespace FoodAssistant.Contracts.Catalog;

/// <summary>Creates or changes a storage location; <paramref name="Id"/> only on create (optional).</summary>
public sealed record StorageLocationRequest(Guid? Id, string Name, StorageTypeDto Type, int? SortOrder);
