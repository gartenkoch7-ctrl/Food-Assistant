using FoodAssistant.Application.ShelfLife;
using FoodAssistant.Domain.Inventory;

namespace FoodAssistant.Application.Tests.ShelfLife;

/// <summary>In-memory override store of one household.</summary>
internal sealed class FakeShelfLifeOverrideStore : IShelfLifeOverrideStore
{
    public Dictionary<Guid, string> ProductCategories { get; } = [];

    public Dictionary<(string Category, StorageType Type), ShelfLifeRule> Categories { get; } = [];

    public Dictionary<(Guid Product, StorageType Type), ShelfLifeRule> Products { get; } = [];

    public Task<ShelfLifeRule?> GetForProductAsync(Guid productId, StorageType storageType, CancellationToken cancellationToken) =>
        Task.FromResult(Products.GetValueOrDefault((productId, storageType)));

    public Task<ShelfLifeRule?> GetForCategoryAsync(string categoryKey, StorageType storageType, CancellationToken cancellationToken) =>
        Task.FromResult(Categories.GetValueOrDefault((categoryKey, storageType)));

    public Task<IReadOnlyList<CategoryShelfLifeOverride>> ListCategoryOverridesAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<CategoryShelfLifeOverride>>(
            [.. Categories.Select(c => new CategoryShelfLifeOverride(c.Key.Category, c.Key.Type, c.Value))]);

    public Task<IReadOnlyDictionary<StorageType, ShelfLifeRule>> ListProductOverridesAsync(Guid productId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyDictionary<StorageType, ShelfLifeRule>>(
            Products.Where(p => p.Key.Product == productId).ToDictionary(p => p.Key.Type, p => p.Value));

    public Task UpsertCategoryAsync(
        Guid householdId,
        string categoryKey,
        StorageType storageType,
        ShelfLifeRule rule,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        Categories[(categoryKey, storageType)] = rule;
        return Task.CompletedTask;
    }

    public Task<bool> UpsertProductAsync(
        Guid householdId,
        Guid productId,
        StorageType storageType,
        ShelfLifeRule rule,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (!ProductCategories.ContainsKey(productId))
        {
            return Task.FromResult(false);
        }

        Products[(productId, storageType)] = rule;
        return Task.FromResult(true);
    }

    public Task DeleteCategoryAsync(string categoryKey, StorageType storageType, CancellationToken cancellationToken)
    {
        Categories.Remove((categoryKey, storageType));
        return Task.CompletedTask;
    }

    public Task DeleteProductAsync(Guid productId, StorageType storageType, CancellationToken cancellationToken)
    {
        Products.Remove((productId, storageType));
        return Task.CompletedTask;
    }

    public Task<string?> GetCategoryOfProductAsync(Guid productId, CancellationToken cancellationToken) =>
        Task.FromResult(ProductCategories.GetValueOrDefault(productId));
}
