using FoodAssistant.Domain.Inventory;

namespace FoodAssistant.Application.ShelfLife;

/// <summary>
/// The real <see cref="IShelfLifeRules"/>: product override, then household category override, then
/// the shipped default, resolved per value (<see cref="ShelfLifeRuleResolver"/>). Without any of them
/// the result is <see cref="ShelfLifeRule.Empty"/>, so no date is computed.
/// </summary>
public sealed class ShelfLifeRuleService(IShelfLifeOverrideStore store, IDefaultShelfLifeRules defaults) : IShelfLifeRules
{
    public async Task<ShelfLifeRule> ResolveAsync(Guid productId, StorageType storageType, CancellationToken cancellationToken)
    {
        var categoryKey = await store.GetCategoryOfProductAsync(productId, cancellationToken).ConfigureAwait(false);
        if (categoryKey is null)
        {
            return ShelfLifeRule.Empty;
        }

        var productOverride = await store.GetForProductAsync(productId, storageType, cancellationToken).ConfigureAwait(false);
        var categoryOverride = await store.GetForCategoryAsync(categoryKey, storageType, cancellationToken).ConfigureAwait(false);
        return ShelfLifeRuleResolver.Resolve(productOverride, categoryOverride, defaults.Find(categoryKey, storageType)?.Rule);
    }

    public async Task<ShelfLifeRule> ResolveForCategoryAsync(string categoryKey, StorageType storageType, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(categoryKey);
        var categoryOverride = await store.GetForCategoryAsync(categoryKey, storageType, cancellationToken).ConfigureAwait(false);
        return ShelfLifeRuleResolver.Resolve(null, categoryOverride, defaults.Find(categoryKey, storageType)?.Rule);
    }
}
