using FoodAssistant.Contracts.Inventory;

namespace FoodAssistant.Contracts.ShelfLife;

/// <summary>One row of a shelf-life table (category or product × storage type).</summary>
/// <param name="CategoryKey">Category of the row (for a product: the product's category).</param>
/// <param name="StorageType">Storage type the values apply to.</param>
/// <param name="Default">
/// Values inherited from the less specific levels: the shipped default for a category row; the
/// category's effective values (household override, else default) for a product row.
/// </param>
/// <param name="Override">The household's own values at this level, or <see langword="null"/> without override.</param>
/// <param name="Effective">Values that apply, resolved per value (override before default).</param>
/// <param name="DefaultSources">Source key per shipped value in <paramref name="Default"/>.</param>
public sealed record ShelfLifeRuleRowDto(
    string CategoryKey,
    StorageTypeDto StorageType,
    ShelfLifeDaysDto Default,
    ShelfLifeDaysDto? Override,
    ShelfLifeDaysDto Effective,
    ShelfLifeSourceKeysDto DefaultSources);
