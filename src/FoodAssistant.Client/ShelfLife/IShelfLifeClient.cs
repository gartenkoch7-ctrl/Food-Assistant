using FoodAssistant.Client.Http;
using FoodAssistant.Contracts.Inventory;
using FoodAssistant.Contracts.ShelfLife;

namespace FoodAssistant.Client.ShelfLife;

/// <summary>
/// Shelf-life rules of one household: shipped defaults with sources and the household's overrides
/// per category and per product. Overrides are upserts without ETag (last write wins).
/// </summary>
public interface IShelfLifeClient
{
    Task<ApiResult<IReadOnlyList<ShelfLifeRuleRowDto>>> ListRulesAsync(Guid householdId, CancellationToken cancellationToken);

    Task<ApiResult<IReadOnlyList<ShelfLifeSourceDto>>> ListSourcesAsync(Guid householdId, CancellationToken cancellationToken);

    /// <summary>Saves the category's own values; all values empty removes the override.</summary>
    Task<ApiResult<ShelfLifeRuleRowDto>> SetCategoryOverrideAsync(
        Guid householdId, string categoryKey, StorageTypeDto storageType, ShelfLifeDaysDto days, CancellationToken cancellationToken);

    Task<ApiResult> ResetCategoryOverrideAsync(
        Guid householdId, string categoryKey, StorageTypeDto storageType, CancellationToken cancellationToken);

    Task<ApiResult<ProductShelfLifeDto>> GetProductAsync(Guid householdId, Guid productId, CancellationToken cancellationToken);

    /// <summary>Saves the product's own values; all values empty removes the override.</summary>
    Task<ApiResult<ShelfLifeRuleRowDto>> SetProductOverrideAsync(
        Guid householdId, Guid productId, StorageTypeDto storageType, ShelfLifeDaysDto days, CancellationToken cancellationToken);

    Task<ApiResult> ResetProductOverrideAsync(
        Guid householdId, Guid productId, StorageTypeDto storageType, CancellationToken cancellationToken);
}
