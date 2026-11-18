using FoodAssistant.Client.Http;
using FoodAssistant.Contracts.Common;
using FoodAssistant.Contracts.Inventory;

namespace FoodAssistant.Client.Inventory;

/// <summary>
/// Stock of one household: overview, batches, consumption and undo. The UI only talks to this
/// interface so the mobile app can swap in a cached variant (ADR-0005). ETags are passed as sent by
/// the server (<c>"42"</c>); <see langword="null"/> means "without If-Match" where the API allows it.
/// </summary>
public interface IInventoryClient
{
    Task<ApiResult<CursorPage<InventoryProductDto>>> ListAsync(
        Guid householdId, string? status, Guid? locationId, string? query, string? cursor, int? limit, CancellationToken cancellationToken);

    Task<ApiResult<InventoryProductDto>> GetProductAsync(Guid householdId, Guid productId, CancellationToken cancellationToken);

    Task<ApiResult<BatchDto>> GetBatchAsync(Guid householdId, Guid batchId, CancellationToken cancellationToken);

    Task<ApiResult<InventoryActionResponse>> AddBatchAsync(Guid householdId, AddBatchRequest request, CancellationToken cancellationToken);

    Task<ApiResult<InventoryActionResponse>> CorrectBatchAsync(
        Guid householdId, Guid batchId, CorrectBatchRequest request, string etag, CancellationToken cancellationToken);

    Task<ApiResult<InventoryActionResponse>> MoveBatchAsync(
        Guid householdId, Guid batchId, MoveBatchRequest request, string etag, CancellationToken cancellationToken);

    Task<ApiResult<InventoryActionResponse>> OpenBatchAsync(Guid householdId, Guid batchId, string etag, CancellationToken cancellationToken);

    Task<ApiResult<InventoryActionResponse>> ConsumeBatchAsync(
        Guid householdId, Guid batchId, ConsumeBatchRequest request, string? etag, CancellationToken cancellationToken);

    Task<ApiResult<InventoryActionResponse>> ConsumeProductAsync(
        Guid householdId, Guid productId, ConsumeProductRequest request, CancellationToken cancellationToken);

    Task<ApiResult<InventoryActionResponse>> CreateLeftoverAsync(
        Guid householdId, CreateLeftoverRequest request, CancellationToken cancellationToken);

    Task<ApiResult<InventoryActionResponse>> UndoAsync(Guid householdId, Guid activityId, CancellationToken cancellationToken);
}
