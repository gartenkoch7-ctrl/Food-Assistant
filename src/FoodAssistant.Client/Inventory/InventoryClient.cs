using System.Globalization;
using System.Text;
using FoodAssistant.Client.Http;
using FoodAssistant.Contracts.Common;
using FoodAssistant.Contracts.Inventory;

namespace FoodAssistant.Client.Inventory;

/// <summary>The inventory API of a household over <see cref="ApiHttpClient"/>; user input is escaped into the route.</summary>
public sealed class InventoryClient(ApiHttpClient api) : IInventoryClient
{
    public Task<ApiResult<CursorPage<InventoryProductDto>>> ListAsync(
        Guid householdId, string? status, Guid? locationId, string? query, string? cursor, int? limit, CancellationToken cancellationToken)
    {
        var parameters = new StringBuilder();
        Append(parameters, "status", status);
        Append(parameters, "locationId", locationId?.ToString("D"));
        Append(parameters, "query", string.IsNullOrWhiteSpace(query) ? null : query);
        Append(parameters, "cursor", cursor);
        Append(parameters, "limit", limit?.ToString(CultureInfo.InvariantCulture));
        return api.GetAsync<CursorPage<InventoryProductDto>>(Path(householdId, "inventory" + parameters), cancellationToken);
    }

    public Task<ApiResult<InventoryProductDto>> GetProductAsync(Guid householdId, Guid productId, CancellationToken cancellationToken) =>
        api.GetAsync<InventoryProductDto>(Path(householdId, $"inventory/products/{productId:D}"), cancellationToken);

    public Task<ApiResult<BatchDto>> GetBatchAsync(Guid householdId, Guid batchId, CancellationToken cancellationToken) =>
        api.GetAsync<BatchDto>(Path(householdId, $"inventory/batches/{batchId:D}"), cancellationToken);

    public Task<ApiResult<InventoryActionResponse>> AddBatchAsync(Guid householdId, AddBatchRequest request, CancellationToken cancellationToken) =>
        SendAsync(HttpMethod.Post, Path(householdId, "inventory/batches"), request, null, cancellationToken);

    public Task<ApiResult<InventoryActionResponse>> CorrectBatchAsync(
        Guid householdId, Guid batchId, CorrectBatchRequest request, string etag, CancellationToken cancellationToken) =>
        SendAsync(HttpMethod.Put, Path(householdId, $"inventory/batches/{batchId:D}"), request, etag, cancellationToken);

    public Task<ApiResult<InventoryActionResponse>> MoveBatchAsync(
        Guid householdId, Guid batchId, MoveBatchRequest request, string etag, CancellationToken cancellationToken) =>
        SendAsync(HttpMethod.Post, Path(householdId, $"inventory/batches/{batchId:D}/move"), request, etag, cancellationToken);

    public Task<ApiResult<InventoryActionResponse>> OpenBatchAsync(Guid householdId, Guid batchId, string etag, CancellationToken cancellationToken) =>
        SendAsync<object>(HttpMethod.Post, Path(householdId, $"inventory/batches/{batchId:D}/open"), null, etag, cancellationToken);

    public Task<ApiResult<InventoryActionResponse>> ConsumeBatchAsync(
        Guid householdId, Guid batchId, ConsumeBatchRequest request, string? etag, CancellationToken cancellationToken) =>
        SendAsync(HttpMethod.Post, Path(householdId, $"inventory/batches/{batchId:D}/consume"), request, etag, cancellationToken);

    public Task<ApiResult<InventoryActionResponse>> ConsumeProductAsync(
        Guid householdId, Guid productId, ConsumeProductRequest request, CancellationToken cancellationToken) =>
        SendAsync(HttpMethod.Post, Path(householdId, $"inventory/products/{productId:D}/consume"), request, null, cancellationToken);

    public Task<ApiResult<InventoryActionResponse>> CreateLeftoverAsync(
        Guid householdId, CreateLeftoverRequest request, CancellationToken cancellationToken) =>
        SendAsync(HttpMethod.Post, Path(householdId, "inventory/leftovers"), request, null, cancellationToken);

    public Task<ApiResult<InventoryActionResponse>> UndoAsync(Guid householdId, Guid activityId, CancellationToken cancellationToken) =>
        SendAsync<object>(HttpMethod.Post, Path(householdId, $"inventory/activities/{activityId:D}/undo"), null, null, cancellationToken);

    private static string Path(Guid householdId, string relative) => ApiPaths.Household(householdId, relative);

    private Task<ApiResult<InventoryActionResponse>> SendAsync<TRequest>(
        HttpMethod method, string path, TRequest? body, string? etag, CancellationToken cancellationToken) =>
        api.SendAsync<TRequest, InventoryActionResponse>(method, path, body, etag, null, cancellationToken);

    private static void Append(StringBuilder parameters, string name, string? value)
    {
        if (value is null)
        {
            return;
        }

        parameters.Append(parameters.Length == 0 ? '?' : '&').Append(name).Append('=').Append(ApiPaths.Escape(value));
    }
}
