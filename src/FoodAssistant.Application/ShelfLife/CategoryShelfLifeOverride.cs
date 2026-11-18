using FoodAssistant.Domain.Inventory;

namespace FoodAssistant.Application.ShelfLife;

/// <summary>A household's own shelf-life values for one category and storage type.</summary>
public sealed record CategoryShelfLifeOverride(string CategoryKey, StorageType StorageType, ShelfLifeRule Rule);
