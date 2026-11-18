using FoodAssistant.Domain.Inventory;

namespace FoodAssistant.Application.ShelfLife;

/// <summary>
/// Household overrides of shelf-life rules per category or product (ADR-0010). Reads see only the
/// active household (query filter); writes set the household passed in.
/// </summary>
public interface IShelfLifeOverrideStore
{
    /// <summary>The product's override for a storage type, or <see langword="null"/>.</summary>
    Task<ShelfLifeRule?> GetForProductAsync(Guid productId, StorageType storageType, CancellationToken cancellationToken);

    /// <summary>The category's override for a storage type, or <see langword="null"/>.</summary>
    Task<ShelfLifeRule?> GetForCategoryAsync(string categoryKey, StorageType storageType, CancellationToken cancellationToken);

    /// <summary>All category overrides of the household.</summary>
    Task<IReadOnlyList<CategoryShelfLifeOverride>> ListCategoryOverridesAsync(CancellationToken cancellationToken);

    /// <summary>The product's overrides by storage type (empty without any).</summary>
    Task<IReadOnlyDictionary<StorageType, ShelfLifeRule>> ListProductOverridesAsync(Guid productId, CancellationToken cancellationToken);

    /// <summary>Creates or replaces the category override (last write wins).</summary>
    Task UpsertCategoryAsync(
        Guid householdId,
        string categoryKey,
        StorageType storageType,
        ShelfLifeRule rule,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    /// <summary>Creates or replaces the product override (last write wins).</summary>
    /// <returns><see langword="false"/> if the product does not exist (or is archived) in the household.</returns>
    Task<bool> UpsertProductAsync(
        Guid householdId,
        Guid productId,
        StorageType storageType,
        ShelfLifeRule rule,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    /// <summary>Removes the category override if there is one.</summary>
    Task DeleteCategoryAsync(string categoryKey, StorageType storageType, CancellationToken cancellationToken);

    /// <summary>Removes the product override if there is one.</summary>
    Task DeleteProductAsync(Guid productId, StorageType storageType, CancellationToken cancellationToken);

    /// <summary>Category of a product that is not archived, or <see langword="null"/> if there is none.</summary>
    Task<string?> GetCategoryOfProductAsync(Guid productId, CancellationToken cancellationToken);
}
