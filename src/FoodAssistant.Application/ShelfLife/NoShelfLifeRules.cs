using FoodAssistant.Domain.Inventory;

namespace FoodAssistant.Application.ShelfLife;

/// <summary>
/// Placeholder until the shelf-life track registers the real rules: always
/// <see cref="ShelfLifeRule.Empty"/>, so no date is computed (safe: no made-up dates).
/// </summary>
public sealed class NoShelfLifeRules : IShelfLifeRules
{
    public Task<ShelfLifeRule> ResolveAsync(Guid productId, StorageType storageType, CancellationToken cancellationToken) =>
        Task.FromResult(ShelfLifeRule.Empty);

    public Task<ShelfLifeRule> ResolveForCategoryAsync(string categoryKey, StorageType storageType, CancellationToken cancellationToken) =>
        Task.FromResult(ShelfLifeRule.Empty);
}
