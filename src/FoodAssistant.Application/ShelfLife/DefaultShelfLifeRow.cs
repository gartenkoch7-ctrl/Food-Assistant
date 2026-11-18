using FoodAssistant.Domain.Inventory;

namespace FoodAssistant.Application.ShelfLife;

/// <summary>One row of the shipped default table: category × storage type with its source.</summary>
public sealed record DefaultShelfLifeRow(string CategoryKey, StorageType StorageType, ShelfLifeRule Rule, string SourceKey);
