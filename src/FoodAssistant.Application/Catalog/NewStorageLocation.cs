using FoodAssistant.Domain.Inventory;

namespace FoodAssistant.Application.Catalog;

/// <summary>A validated new storage location; <paramref name="SortOrder"/> null appends it at the end.</summary>
public sealed record NewStorageLocation(Guid Id, Guid HouseholdId, string Name, StorageType Type, int? SortOrder, DateTimeOffset Now);
