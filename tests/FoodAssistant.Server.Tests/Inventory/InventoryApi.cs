using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using FoodAssistant.Contracts.Common;
using FoodAssistant.Contracts.Inventory;
using FoodAssistant.Server.Tests.Auth;

namespace FoodAssistant.Server.Tests.Inventory;

/// <summary>Calls of the inventory API used across the inventory tests.</summary>
internal static class InventoryApi
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public static AddBatchRequest Add(
        Guid productId,
        Guid locationId,
        decimal amount = 1m,
        string unit = "l",
        DateOnly? date = null,
        ExpiryKindDto? kind = null,
        bool selfFrozen = false) =>
        new(null, productId, locationId, new QuantityDto(amount, unit), date,
            kind ?? (date is null ? ExpiryKindDto.None : ExpiryKindDto.BestBefore), BatchOriginDto.Manual, selfFrozen);

    public static async Task<BatchDto> AddAsync(InventoryApp app, AddBatchRequest request, TestClient? client = null)
    {
        using var response = await (client ?? app.Owner).PostAsync($"{app.Inventory}/batches", request);
        var result = await TestClient.ReadAsync<InventoryActionResponse>(response, HttpStatusCode.Created);
        return Assert.Single(result.Batches);
    }

    /// <summary>Sends a request with an optional <c>If-Match</c> version.</summary>
    public static Task<HttpResponseMessage> SendAsync(
        TestClient client,
        HttpMethod method,
        string url,
        object? body = null,
        long? ifMatch = null)
    {
        ArgumentNullException.ThrowIfNull(client);
        var request = new HttpRequestMessage(method, new Uri(url, UriKind.Relative));
        if (body is not null)
        {
            request.Content = JsonContent.Create(body, body.GetType());
        }

        if (ifMatch is { } version)
        {
            request.Headers.TryAddWithoutValidation("If-Match", $"\"{version.ToString(CultureInfo.InvariantCulture)}\"");
        }

        return client.Http.SendAsync(request, Ct);
    }

    public static async Task<InventoryActionResponse> ActAsync(
        TestClient client,
        HttpMethod method,
        string url,
        object? body = null,
        long? ifMatch = null)
    {
        using var response = await SendAsync(client, method, url, body, ifMatch);
        return await TestClient.ReadAsync<InventoryActionResponse>(response);
    }

    public static Task<BatchDto> GetBatchAsync(InventoryApp app, Guid batchId) =>
        app.Owner.GetJsonAsync<BatchDto>($"{app.Inventory}/batches/{batchId}");

    public static Task<InventoryActionResponse> UndoAsync(InventoryApp app, Guid activityId) =>
        ActAsync(app.Owner, HttpMethod.Post, $"{app.Inventory}/activities/{activityId}/undo");

    public static Task<HttpResponseMessage> TryUndoAsync(InventoryApp app, Guid activityId) =>
        SendAsync(app.Owner, HttpMethod.Post, $"{app.Inventory}/activities/{activityId}/undo");
}
