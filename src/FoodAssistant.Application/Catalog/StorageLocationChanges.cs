using FoodAssistant.Domain.Inventory;

namespace FoodAssistant.Application.Catalog;

/// <summary>Validated replacement of a storage location; <paramref name="SortOrder"/> null keeps the current one.</summary>
public sealed record StorageLocationChanges(string Name, StorageType Type, int? SortOrder, DateTimeOffset Now);
