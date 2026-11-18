using FoodAssistant.Application.ShelfLife;
using FoodAssistant.Domain.Inventory;

namespace FoodAssistant.Server.Tests.Inventory;

/// <summary>Shelf-life rules a test sets per storage type; everything else has no rule.</summary>
public sealed class FakeShelfLifeRules : IShelfLifeRules
{
    private readonly Dictionary<StorageType, ShelfLifeRule> _rules = [];

    public FakeShelfLifeRules Set(StorageType storageType, ShelfLifeRule rule)
    {
        lock (_rules)
        {
            _rules[storageType] = rule;
        }

        return this;
    }

    public Task<ShelfLifeRule> ResolveAsync(Guid productId, StorageType storageType, CancellationToken cancellationToken) =>
        Task.FromResult(Get(storageType));

    public Task<ShelfLifeRule> ResolveForCategoryAsync(string categoryKey, StorageType storageType, CancellationToken cancellationToken) =>
        Task.FromResult(Get(storageType));

    private ShelfLifeRule Get(StorageType storageType)
    {
        lock (_rules)
        {
            return _rules.GetValueOrDefault(storageType, ShelfLifeRule.Empty);
        }
    }
}
