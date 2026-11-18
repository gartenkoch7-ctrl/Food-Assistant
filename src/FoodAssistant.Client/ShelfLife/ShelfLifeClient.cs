using FoodAssistant.Client.Http;
using FoodAssistant.Contracts.Inventory;
using FoodAssistant.Contracts.ShelfLife;

namespace FoodAssistant.Client.ShelfLife;

public sealed class ShelfLifeClient(ApiHttpClient api) : IShelfLifeClient
{
    public Task<ApiResult<IReadOnlyList<ShelfLifeRuleRowDto>>> ListRulesAsync(Guid householdId, CancellationToken cancellationToken) =>
        api.GetAsync<IReadOnlyList<ShelfLifeRuleRowDto>>(ApiPaths.Household(householdId, "shelf-life/rules"), cancellationToken);

    public Task<ApiResult<IReadOnlyList<ShelfLifeSourceDto>>> ListSourcesAsync(Guid householdId, CancellationToken cancellationToken) =>
        api.GetAsync<IReadOnlyList<ShelfLifeSourceDto>>(ApiPaths.Household(householdId, "shelf-life/sources"), cancellationToken);

    public Task<ApiResult<ShelfLifeRuleRowDto>> SetCategoryOverrideAsync(
        Guid householdId, string categoryKey, StorageTypeDto storageType, ShelfLifeDaysDto days, CancellationToken cancellationToken) =>
        api.SendAsync<ShelfLifeDaysDto, ShelfLifeRuleRowDto>(
            HttpMethod.Put, CategoryPath(householdId, categoryKey, storageType), days, null, null, cancellationToken);

    public Task<ApiResult> ResetCategoryOverrideAsync(
        Guid householdId, string categoryKey, StorageTypeDto storageType, CancellationToken cancellationToken) =>
        api.SendAsync<object>(HttpMethod.Delete, CategoryPath(householdId, categoryKey, storageType), null, null, null, cancellationToken);

    public Task<ApiResult<ProductShelfLifeDto>> GetProductAsync(Guid householdId, Guid productId, CancellationToken cancellationToken) =>
        api.GetAsync<ProductShelfLifeDto>(ApiPaths.Household(householdId, $"products/{productId:D}/shelf-life"), cancellationToken);

    public Task<ApiResult<ShelfLifeRuleRowDto>> SetProductOverrideAsync(
        Guid householdId, Guid productId, StorageTypeDto storageType, ShelfLifeDaysDto days, CancellationToken cancellationToken) =>
        api.SendAsync<ShelfLifeDaysDto, ShelfLifeRuleRowDto>(
            HttpMethod.Put, ProductPath(householdId, productId, storageType), days, null, null, cancellationToken);

    public Task<ApiResult> ResetProductOverrideAsync(
        Guid householdId, Guid productId, StorageTypeDto storageType, CancellationToken cancellationToken) =>
        api.SendAsync<object>(HttpMethod.Delete, ProductPath(householdId, productId, storageType), null, null, null, cancellationToken);

    private static string CategoryPath(Guid householdId, string categoryKey, StorageTypeDto storageType)
    {
        ArgumentNullException.ThrowIfNull(categoryKey);
        return ApiPaths.Household(
            householdId, $"shelf-life/overrides/categories/{ApiPaths.Escape(categoryKey)}/{Segment(storageType)}");
    }

    private static string ProductPath(Guid householdId, Guid productId, StorageTypeDto storageType) =>
        ApiPaths.Household(householdId, $"products/{productId:D}/shelf-life/{Segment(storageType)}");

    // The route takes the JSON names of the enum (chilled, frozen, ambient).
    private static string Segment(StorageTypeDto storageType) => storageType switch
    {
        StorageTypeDto.Chilled => "chilled",
        StorageTypeDto.Frozen => "frozen",
        StorageTypeDto.Ambient => "ambient",
        _ => throw new ArgumentOutOfRangeException(nameof(storageType), storageType, "Unknown storage type."),
    };
}
