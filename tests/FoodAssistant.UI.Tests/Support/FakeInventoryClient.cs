using FoodAssistant.Client.Http;
using FoodAssistant.Client.Inventory;
using FoodAssistant.Contracts.Common;
using FoodAssistant.Contracts.Inventory;

namespace FoodAssistant.UI.Tests.Support;

/// <summary>
/// Inventory client over an in-memory product list. Reads filter by status and name like the server;
/// actions are recorded and answer <see cref="NextAction"/> (or fail with <see cref="FailWith"/>).
/// </summary>
public sealed class FakeInventoryClient : IInventoryClient
{
    public List<InventoryProductDto> Products { get; } = [];

    public List<string> Calls { get; } = [];

    /// <summary>Request bodies of the actions, in order.</summary>
    public List<object> Requests { get; } = [];

    /// <summary>When set, every action call fails with it.</summary>
    public ApiError? FailWith { get; set; }

    public InventoryActionResponse NextAction { get; set; } = new([], [], Guid.CreateVersion7());

    public Task<ApiResult<CursorPage<InventoryProductDto>>> ListAsync(
        Guid householdId, string? status, Guid? locationId, string? query, string? cursor, int? limit, CancellationToken cancellationToken)
    {
        Calls.Add($"list:{status}:{locationId}:{query}");
        var items = Products
            .Where(p => string.IsNullOrWhiteSpace(query) || p.Product.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
            .Where(p => status is null || p.Batches.Any(b => StatusKey(b.Status) == status))
            .ToList();
        return Task.FromResult(ApiResult<CursorPage<InventoryProductDto>>.Success(new CursorPage<InventoryProductDto>(items, null)));
    }

    public Task<ApiResult<InventoryProductDto>> GetProductAsync(Guid householdId, Guid productId, CancellationToken cancellationToken)
    {
        Calls.Add($"product:{productId}");
        return Task.FromResult(Products.FirstOrDefault(p => p.Product.Id == productId) is { } product
            ? ApiResult<InventoryProductDto>.Success(product)
            : ApiResult<InventoryProductDto>.Failure(NotFound("catalog.product_not_found")));
    }

    public Task<ApiResult<BatchDto>> GetBatchAsync(Guid householdId, Guid batchId, CancellationToken cancellationToken)
    {
        Calls.Add($"batch:{batchId}");
        return Task.FromResult(Products.SelectMany(p => p.Batches).FirstOrDefault(b => b.Id == batchId) is { } batch
            ? ApiResult<BatchDto>.Success(batch, $"\"{batch.Version}\"")
            : ApiResult<BatchDto>.Failure(NotFound("inventory.batch_not_found")));
    }

    public Task<ApiResult<InventoryActionResponse>> AddBatchAsync(Guid householdId, AddBatchRequest request, CancellationToken cancellationToken) =>
        Act("add", request, null);

    public Task<ApiResult<InventoryActionResponse>> CorrectBatchAsync(
        Guid householdId, Guid batchId, CorrectBatchRequest request, string etag, CancellationToken cancellationToken) =>
        Act($"correct:{batchId}", request, etag);

    public Task<ApiResult<InventoryActionResponse>> MoveBatchAsync(
        Guid householdId, Guid batchId, MoveBatchRequest request, string etag, CancellationToken cancellationToken) =>
        Act($"move:{batchId}", request, etag);

    public Task<ApiResult<InventoryActionResponse>> OpenBatchAsync(Guid householdId, Guid batchId, string etag, CancellationToken cancellationToken) =>
        Act($"open:{batchId}", null, etag);

    public Task<ApiResult<InventoryActionResponse>> ConsumeBatchAsync(
        Guid householdId, Guid batchId, ConsumeBatchRequest request, string? etag, CancellationToken cancellationToken) =>
        Act($"consume:{batchId}", request, etag);

    public Task<ApiResult<InventoryActionResponse>> ConsumeProductAsync(
        Guid householdId, Guid productId, ConsumeProductRequest request, CancellationToken cancellationToken) =>
        Act($"consume-product:{productId}", request, null);

    public Task<ApiResult<InventoryActionResponse>> CreateLeftoverAsync(
        Guid householdId, CreateLeftoverRequest request, CancellationToken cancellationToken) =>
        Act("leftover", request, null);

    public Task<ApiResult<InventoryActionResponse>> UndoAsync(Guid householdId, Guid activityId, CancellationToken cancellationToken) =>
        Act($"undo:{activityId}", null, null);

    private Task<ApiResult<InventoryActionResponse>> Act(string call, object? request, string? etag)
    {
        Calls.Add(etag is null ? call : $"{call}@{etag}");
        if (request is not null)
        {
            Requests.Add(request);
        }

        return Task.FromResult(FailWith is null
            ? ApiResult<InventoryActionResponse>.Success(NextAction)
            : ApiResult<InventoryActionResponse>.Failure(FailWith));
    }

    private static string StatusKey(ExpiryStatusDto status) => status switch
    {
        ExpiryStatusDto.Expired => "expired",
        ExpiryStatusDto.Soon => "soon",
        _ => "other",
    };

    private static ApiError NotFound(string code) => FakeAccountClient.Error(404, code);
}
