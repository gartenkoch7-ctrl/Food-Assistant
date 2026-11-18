using System.Net;
using FoodAssistant.Client.Http;
using FoodAssistant.Client.Inventory;
using FoodAssistant.Client.Tests.Support;
using FoodAssistant.Contracts.Common;
using FoodAssistant.Contracts.Inventory;

namespace FoodAssistant.Client.Tests.Inventory;

public sealed class InventoryClientTests
{
    private static readonly Guid Household = Guid.Parse("0197a0c0-0000-7000-8000-000000000001");
    private static readonly Guid Id = Guid.Parse("0197a0c0-0000-7000-8000-000000000002");
    private static string Base => $"https://food.test/api/v1/households/{Household:D}/inventory";

    private const string Action = """{"batches":[],"removedBatchIds":["0197a0c0-0000-7000-8000-000000000002"],"activityId":"0197a0c0-0000-7000-8000-000000000003"}""";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static (InventoryClient Client, FakeHttpHandler Handler) Create()
    {
        var handler = new FakeHttpHandler();
        var api = new ApiHttpClient(new HttpClient(handler) { BaseAddress = new Uri("https://food.test/") });
        return (new InventoryClient(api), handler);
    }

    [Fact]
    public async Task List_SendsFiltersEscaped_AndOmitsEmptyOnes()
    {
        var (client, handler) = Create();
        handler.EnqueueJson(HttpStatusCode.OK, """{"items":[],"nextCursor":null}""").EnqueueJson(HttpStatusCode.OK, """{"items":[],"nextCursor":null}""");

        await client.ListAsync(Household, "soon", Id, "a&b", "cur sor", 20, Ct);
        await client.ListAsync(Household, null, null, " ", null, null, Ct);

        Assert.Equal($"{Base}?status=soon&locationId={Id:D}&query=a%26b&cursor=cur%20sor&limit=20", handler.Requests[0].Uri.AbsoluteUri);
        Assert.Equal(Base, handler.Requests[1].Uri.AbsoluteUri);
    }

    [Fact]
    public async Task Changes_SendIfMatchWhereGiven_AndReadTheActivity()
    {
        var (client, handler) = Create();
        for (var i = 0; i < 4; i++)
        {
            handler.EnqueueJson(HttpStatusCode.OK, Action);
        }

        var corrected = await client.CorrectBatchAsync(Household, Id, new CorrectBatchRequest(new QuantityDto(1m, "l"), null, ExpiryKindDto.None), "\"3\"", Ct);
        await client.MoveBatchAsync(Household, Id, new MoveBatchRequest(Id), "\"4\"", Ct);
        await client.OpenBatchAsync(Household, Id, "\"5\"", Ct);
        await client.ConsumeBatchAsync(Household, Id, new ConsumeBatchRequest(ConsumptionKindDto.UsedUp, null, null), null, Ct);

        Assert.Equal(Guid.Parse("0197a0c0-0000-7000-8000-000000000003"), corrected.Value!.ActivityId);
        Assert.Equal([HttpMethod.Put, HttpMethod.Post, HttpMethod.Post, HttpMethod.Post], handler.Requests.Select(r => r.Method));
        Assert.Equal(["\"3\"", "\"4\"", "\"5\""], handler.Requests.Take(3).Select(r => r.Headers["If-Match"]));
        Assert.False(handler.Requests[3].Headers.ContainsKey("If-Match"));
        Assert.Equal(
            [$"{Base}/batches/{Id:D}", $"{Base}/batches/{Id:D}/move", $"{Base}/batches/{Id:D}/open", $"{Base}/batches/{Id:D}/consume"],
            handler.Requests.Select(r => r.Uri.AbsoluteUri));
        Assert.Contains("\"kind\":\"used_up\"", handler.Requests[3].Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Creates_ProductConsume_AndUndo_UseTheirRoutes()
    {
        var (client, handler) = Create();
        for (var i = 0; i < 4; i++)
        {
            handler.EnqueueJson(i < 2 ? HttpStatusCode.Created : HttpStatusCode.OK, Action);
        }

        await client.AddBatchAsync(Household, new AddBatchRequest(null, Id, Id, new QuantityDto(1m, "l"), null, ExpiryKindDto.None, BatchOriginDto.Manual, false), Ct);
        await client.CreateLeftoverAsync(Household, new CreateLeftoverRequest(null, Id, Id, new QuantityDto(1m, "l")), Ct);
        await client.ConsumeProductAsync(Household, Id, new ConsumeProductRequest(ConsumptionKindDto.Discarded, null, ConsumptionReasonDto.Spoiled), Ct);
        await client.UndoAsync(Household, Id, Ct);

        Assert.Equal(
            [$"{Base}/batches", $"{Base}/leftovers", $"{Base}/products/{Id:D}/consume", $"{Base}/activities/{Id:D}/undo"],
            handler.Requests.Select(r => r.Uri.AbsoluteUri));
        Assert.All(handler.Requests, r => Assert.Equal(HttpMethod.Post, r.Method));
        Assert.All(handler.Requests, r => Assert.True(r.Headers.ContainsKey("Idempotency-Key")));
    }

    [Fact]
    public async Task Reads_ProductAndBatch_WithEtag()
    {
        var (client, handler) = Create();
        const string batchJson =
            """{"id":"0197a0c0-0000-7000-8000-000000000002","productId":"0197a0c0-0000-7000-8000-000000000002","storageLocationId":"0197a0c0-0000-7000-8000-000000000002","quantity":{"value":1,"unit":"l"},"storageType":"chilled","state":"sealed","expiresOn":null,"expiryKind":"none","packageExpiresOn":null,"packageExpiryKind":"none","openedOn":null,"frozenOn":null,"thawedOn":null,"preparedOn":null,"origin":"manual","status":0,"daysUntilExpiry":null,"version":9}""";
        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(batchJson, System.Text.Encoding.UTF8, "application/json"),
            Headers = { ETag = new System.Net.Http.Headers.EntityTagHeaderValue("\"9\"") },
        });
        handler.EnqueueStatus(HttpStatusCode.NotFound);

        var batch = await client.GetBatchAsync(Household, Id, Ct);
        var product = await client.GetProductAsync(Household, Id, Ct);

        Assert.Equal($"{Base}/batches/{Id:D}", handler.Requests[0].Uri.AbsoluteUri);
        Assert.Equal(("\"9\"", 9L, BatchStateDto.Sealed), (batch.ETag, batch.Value!.Version, batch.Value.State));
        Assert.Equal($"{Base}/products/{Id:D}", handler.Requests[1].Uri.AbsoluteUri);
        Assert.Equal(404, product.Error!.Status);
    }
}
