using FoodAssistant.Domain.Inventory;

namespace FoodAssistant.Application.ShelfLife;

/// <summary>
/// Shelf-life rules of the active household: checked defaults per category and storage type plus
/// the household's overrides (docs/kb/vorrat-und-haltbarkeit.md). Never from the AI (AGENTS.md 2.3).
/// </summary>
public interface IShelfLifeRules
{
    /// <summary>Rule for a product in a storage type (product override, else category rule).</summary>
    Task<ShelfLifeRule> ResolveAsync(Guid productId, StorageType storageType, CancellationToken cancellationToken);

    /// <summary>Rule for a category in a storage type (household override, else default).</summary>
    Task<ShelfLifeRule> ResolveForCategoryAsync(string categoryKey, StorageType storageType, CancellationToken cancellationToken);
}
