using System.Net;
using FoodAssistant.Client.Http;
using FoodAssistant.Client.Stores;
using FoodAssistant.Client.Tests.Support;
using FoodAssistant.Contracts.Stores;

namespace FoodAssistant.Client.Tests.Stores;

public sealed class StoreClientTests
{
    private static readonly Guid Household = Guid.Parse("0197a0c0-0000-7000-8000-000000000001");
    private static readonly Guid Store = Guid.Parse("0197a0c0-0000-7000-8000-000000000002");
    private static string Base => $"https://food.test/api/v1/households/{Household:D}/";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private const string StoreJson = """{"id":"0197a0c0-0000-7000-8000-000000000002","name":"Market","version":2}""";

    private const string OrderJson =
        """{"storeId":"0197a0c0-0000-7000-8000-000000000002","sections":["produce","dairy"],"hasManualOrder":true}""";

    private static (StoreClient Client, FakeHttpHandler Handler) Create()
    {
        var handler = new FakeHttpHandler();
        var api = new ApiHttpClient(new HttpClient(handler) { BaseAddress = new Uri("https://food.test/") });
        return (new StoreClient(api), handler);
    }

    [Fact]
    public async Task ListStores_GetsTheStores()
    {
        var (client, handler) = Create();
        handler.EnqueueJson(HttpStatusCode.OK, $"[{StoreJson}]");

        var result = await client.ListStoresAsync(Household, Ct);

        Assert.Equal(Base + "stores", handler.Requests[0].Uri.AbsoluteUri);
        Assert.Equal("Market", result.Value![0].Name);
    }

    [Fact]
    public async Task CreateStore_PostsWithoutIfMatch()
    {
        var (client, handler) = Create();
        handler.EnqueueJson(HttpStatusCode.Created, StoreJson);

        await client.CreateStoreAsync(Household, new StoreRequest(null, "Market"), Ct);

        Assert.Equal(HttpMethod.Post, handler.Requests[0].Method);
        Assert.False(handler.Requests[0].Headers.ContainsKey("If-Match"));
        Assert.Contains("\"name\":\"Market\"", handler.Requests[0].Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UpdateStore_SendsPutWithIfMatch()
    {
        var (client, handler) = Create();
        handler.EnqueueJson(HttpStatusCode.OK, StoreJson);

        await client.UpdateStoreAsync(Household, Store, new StoreRequest(null, "Market"), "\"2\"", Ct);

        Assert.Equal(HttpMethod.Put, handler.Requests[0].Method);
        Assert.Equal(Base + $"stores/{Store:D}", handler.Requests[0].Uri.AbsoluteUri);
        Assert.Equal("\"2\"", handler.Requests[0].Headers["If-Match"]);
    }

    [Fact]
    public async Task DeleteStore_SendsDeleteWithIfMatch()
    {
        var (client, handler) = Create();
        handler.EnqueueStatus(HttpStatusCode.NoContent);

        var result = await client.DeleteStoreAsync(Household, Store, "\"2\"", Ct);

        Assert.True(result.IsSuccess);
        Assert.Equal(HttpMethod.Delete, handler.Requests[0].Method);
        Assert.Equal("\"2\"", handler.Requests[0].Headers["If-Match"]);
    }

    [Fact]
    public async Task GetSectionOrder_OfAStore_UsesTheStoreRoute()
    {
        var (client, handler) = Create();
        handler.EnqueueJson(HttpStatusCode.OK, OrderJson);

        var result = await client.GetSectionOrderAsync(Household, Store, Ct);

        Assert.Equal(Base + $"stores/{Store:D}/section-order", handler.Requests[0].Uri.AbsoluteUri);
        Assert.True(result.Value!.HasManualOrder);
        Assert.Equal(["produce", "dairy"], result.Value.Sections);
    }

    [Fact]
    public async Task GetSectionOrder_WithoutStore_UsesTheDefaultSections()
    {
        var (client, handler) = Create();
        handler.EnqueueJson(HttpStatusCode.OK, """{"storeId":null,"sections":["produce"],"hasManualOrder":false}""");

        var result = await client.GetSectionOrderAsync(Household, null, Ct);

        Assert.Equal(Base + "shopping/sections", handler.Requests[0].Uri.AbsoluteUri);
        Assert.False(result.Value!.HasManualOrder);
    }

    [Fact]
    public async Task SetSectionOrder_PutsTheFullOrder()
    {
        var (client, handler) = Create();
        handler.EnqueueJson(HttpStatusCode.OK, OrderJson);

        await client.SetSectionOrderAsync(Household, Store, new SetSectionOrderRequest(["dairy", "produce"]), Ct);

        Assert.Equal(HttpMethod.Put, handler.Requests[0].Method);
        Assert.Equal(Base + $"stores/{Store:D}/section-order", handler.Requests[0].Uri.AbsoluteUri);
        Assert.Contains("\"sections\":[\"dairy\",\"produce\"]", handler.Requests[0].Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ResetSectionOrder_SendsDelete_AndReadsTheLearnedOrder()
    {
        var (client, handler) = Create();
        handler.EnqueueJson(HttpStatusCode.OK, """{"storeId":"0197a0c0-0000-7000-8000-000000000002","sections":["dairy"],"hasManualOrder":false}""");

        var result = await client.ResetSectionOrderAsync(Household, Store, Ct);

        Assert.Equal(HttpMethod.Delete, handler.Requests[0].Method);
        Assert.Equal(Base + $"stores/{Store:D}/section-order", handler.Requests[0].Uri.AbsoluteUri);
        Assert.False(result.Value!.HasManualOrder);
    }
}
