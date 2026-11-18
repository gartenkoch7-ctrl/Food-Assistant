using FoodAssistant.Client.Http;
using FoodAssistant.Client.ShelfLife;
using FoodAssistant.Contracts.Inventory;
using FoodAssistant.Contracts.ShelfLife;

namespace FoodAssistant.UI.Tests.Support;

/// <summary>Shelf-life client over in-memory rows; resolves like the server (own value before inherited, per value).</summary>
public sealed class FakeShelfLifeClient : IShelfLifeClient
{
    public static readonly ShelfLifeDaysDto None = new(null, null, null, null);

    public List<ShelfLifeRuleRowDto> Rules { get; } = [];

    public List<ShelfLifeSourceDto> Sources { get; } = [];

    public Dictionary<Guid, ProductShelfLifeDto> Products { get; } = [];

    public List<string> Calls { get; } = [];

    /// <summary>When set, every call fails with it.</summary>
    public ApiError? FailWith { get; set; }

    public static ShelfLifeRuleRowDto Row(
        string category, StorageTypeDto type, ShelfLifeDaysDto? inherited = null, ShelfLifeDaysDto? own = null, string? source = null)
    {
        var baseValues = inherited ?? None;
        var sources = new ShelfLifeSourceKeysDto(
            baseValues.DaysAfterOpening is null ? null : source,
            baseValues.DaysFrozen is null ? null : source,
            baseValues.DaysAfterThawing is null ? null : source,
            baseValues.DaysAsLeftover is null ? null : source);
        return new ShelfLifeRuleRowDto(category, type, baseValues, own, Resolve(own, baseValues), sources);
    }

    public Task<ApiResult<IReadOnlyList<ShelfLifeRuleRowDto>>> ListRulesAsync(Guid householdId, CancellationToken cancellationToken) =>
        Respond<IReadOnlyList<ShelfLifeRuleRowDto>>("rules", () => [.. Rules]);

    public Task<ApiResult<IReadOnlyList<ShelfLifeSourceDto>>> ListSourcesAsync(Guid householdId, CancellationToken cancellationToken) =>
        Respond<IReadOnlyList<ShelfLifeSourceDto>>("sources", () => [.. Sources]);

    public Task<ApiResult<ShelfLifeRuleRowDto>> SetCategoryOverrideAsync(
        Guid householdId, string categoryKey, StorageTypeDto storageType, ShelfLifeDaysDto days, CancellationToken cancellationToken) =>
        Respond($"set {categoryKey}/{storageType}", () =>
        {
            var index = Rules.FindIndex(r => r.CategoryKey == categoryKey && r.StorageType == storageType);
            Rules[index] = Apply(Rules[index], days);
            return Rules[index];
        });

    public Task<ApiResult> ResetCategoryOverrideAsync(
        Guid householdId, string categoryKey, StorageTypeDto storageType, CancellationToken cancellationToken)
    {
        Calls.Add($"reset {categoryKey}/{storageType}");
        return Task.FromResult(FailWith is null ? ApiResult.Success() : ApiResult.Failure(FailWith));
    }

    public Task<ApiResult<ProductShelfLifeDto>> GetProductAsync(Guid householdId, Guid productId, CancellationToken cancellationToken) =>
        Products.TryGetValue(productId, out var product)
            ? Respond("product", () => product)
            : Task.FromResult(ApiResult<ProductShelfLifeDto>.Failure(
                new ApiError(404, "catalog.product_not_found", null, new Dictionary<string, string[]>(), null)));

    public Task<ApiResult<ShelfLifeRuleRowDto>> SetProductOverrideAsync(
        Guid householdId, Guid productId, StorageTypeDto storageType, ShelfLifeDaysDto days, CancellationToken cancellationToken) =>
        Respond($"set product/{storageType}", () =>
        {
            var product = Products[productId];
            var row = Apply(product.Rows.Single(r => r.StorageType == storageType), days);
            Products[productId] = product with { Rows = [.. product.Rows.Select(r => r.StorageType == storageType ? row : r)] };
            return row;
        });

    public Task<ApiResult> ResetProductOverrideAsync(
        Guid householdId, Guid productId, StorageTypeDto storageType, CancellationToken cancellationToken)
    {
        Calls.Add($"reset product/{storageType}");
        return Task.FromResult(FailWith is null ? ApiResult.Success() : ApiResult.Failure(FailWith));
    }

    private static ShelfLifeRuleRowDto Apply(ShelfLifeRuleRowDto row, ShelfLifeDaysDto days)
    {
        var own = days == None ? null : days;
        return row with { Override = own, Effective = Resolve(own, row.Default) };
    }

    private static ShelfLifeDaysDto Resolve(ShelfLifeDaysDto? own, ShelfLifeDaysDto inherited) => new(
        own?.DaysAfterOpening ?? inherited.DaysAfterOpening,
        own?.DaysFrozen ?? inherited.DaysFrozen,
        own?.DaysAfterThawing ?? inherited.DaysAfterThawing,
        own?.DaysAsLeftover ?? inherited.DaysAsLeftover);

    private Task<ApiResult<T>> Respond<T>(string call, Func<T> answer)
    {
        Calls.Add(call);
        return Task.FromResult(FailWith is null ? ApiResult<T>.Success(answer()) : ApiResult<T>.Failure(FailWith));
    }
}
