using System.Net;
using FoodAssistant.Client.Http;
using FoodAssistant.Client.Shopping;
using FoodAssistant.Client.Tests.Support;
using FoodAssistant.Contracts.Common;
using FoodAssistant.Contracts.Shopping;

namespace FoodAssistant.Client.Tests.Shopping;

public sealed class ShoppingClientTests
{
    private static readonly Guid Household = Guid.Parse("0197a0c0-0000-7000-8000-000000000001");
    private static readonly Guid List = Guid.Parse("0197a0c0-0000-7000-8000-000000000002");
    private static readonly Guid Item = Guid.Parse("0197a0c0-0000-7000-8000-000000000003");
    private static string Base => $"https://food.test/api/v1/households/{Household:D}/";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private const string ListJson =
        """{"id":"0197a0c0-0000-7000-8000-000000000002","name":"","storeId":null,"isDefault":true,"sortOrder":0,"openItemCount":2,"checkedItemCount":1,"version":4}""";

    private const string ItemJson =
        """{"id":"0197a0c0-0000-7000-8000-000000000003","listId":"0197a0c0-0000-7000-8000-000000000002","productId":null,"text":"Milk","quantity":{"value":2,"unit":"l"},"note":null,"sectionKey":"dairy","origin":"minimum_stock","checked":false,"checkedAt":null,"version":7,"updatedAt":"2026-11-18T10:00:00+00:00"}""";

    private static (ShoppingClient Client, FakeHttpHandler Handler) Create()
    {
        var handler = new FakeHttpHandler();
        var api = new ApiHttpClient(new HttpClient(handler) { BaseAddress = new Uri("https://food.test/") });
        return (new ShoppingClient(api), handler);
    }

    [Fact]
    public async Task ListLists_GetsTheListsOfTheHousehold()
    {
        var (client, handler) = Create();
        handler.EnqueueJson(HttpStatusCode.OK, $"[{ListJson}]");

        var result = await client.ListListsAsync(Household, Ct);

        Assert.Equal(HttpMethod.Get, handler.Requests[0].Method);
        Assert.Equal(Base + "shopping/lists", handler.Requests[0].Uri.AbsoluteUri);
        Assert.True(result.Value![0].IsDefault);
        Assert.Equal(2, result.Value[0].OpenItemCount);
    }

    [Fact]
    public async Task CreateList_PostsTheRequestWithoutIfMatch()
    {
        var (client, handler) = Create();
        handler.EnqueueJson(HttpStatusCode.Created, ListJson);

        var result = await client.CreateListAsync(Household, new CreateShoppingListRequest(null, "Weekend", null), Ct);

        Assert.True(result.IsSuccess);
        Assert.Equal(HttpMethod.Post, handler.Requests[0].Method);
        Assert.Equal(Base + "shopping/lists", handler.Requests[0].Uri.AbsoluteUri);
        Assert.False(handler.Requests[0].Headers.ContainsKey("If-Match"));
        Assert.Contains("\"name\":\"Weekend\"", handler.Requests[0].Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UpdateList_SendsPutWithIfMatch()
    {
        var (client, handler) = Create();
        handler.EnqueueJson(HttpStatusCode.OK, ListJson);

        await client.UpdateListAsync(Household, List, new UpdateShoppingListRequest("Weekend", null, true, 0), "\"4\"", Ct);

        Assert.Equal(HttpMethod.Put, handler.Requests[0].Method);
        Assert.Equal(Base + $"shopping/lists/{List:D}", handler.Requests[0].Uri.AbsoluteUri);
        Assert.Equal("\"4\"", handler.Requests[0].Headers["If-Match"]);
        Assert.Contains("\"isDefault\":true", handler.Requests[0].Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DeleteList_SendsDeleteWithIfMatch_AndReportsTheDefaultListConflict()
    {
        var (client, handler) = Create();
        handler.EnqueueJson(HttpStatusCode.Conflict, """{"errorCode":"shopping.default_list"}""", "application/problem+json");

        var result = await client.DeleteListAsync(Household, List, "\"4\"", Ct);

        Assert.Equal(HttpMethod.Delete, handler.Requests[0].Method);
        Assert.Equal("\"4\"", handler.Requests[0].Headers["If-Match"]);
        Assert.Equal(ShoppingErrorCodes.DefaultList, result.Error!.ErrorCode);
    }

    [Theory]
    [InlineData(true, "true")]
    [InlineData(false, "false")]
    public async Task ListItems_PassesIncludeChecked(bool includeChecked, string expected)
    {
        var (client, handler) = Create();
        handler.EnqueueJson(HttpStatusCode.OK, $"[{ItemJson}]");

        var result = await client.ListItemsAsync(Household, List, includeChecked, Ct);

        Assert.Equal(Base + $"shopping/lists/{List:D}/items?includeChecked={expected}", handler.Requests[0].Uri.AbsoluteUri);
        Assert.Equal(ShoppingItemOriginDto.MinimumStock, result.Value![0].Origin);
        Assert.Equal(7, result.Value[0].Version);
    }

    [Fact]
    public async Task AddItem_PostsTheClientGeneratedId()
    {
        var (client, handler) = Create();
        handler.EnqueueJson(HttpStatusCode.Created, ItemJson);

        var result = await client.AddItemAsync(
            Household, List, new CreateShoppingItemRequest(Item, null, "Milk", new QuantityDto(2, "l"), null, null), Ct);

        Assert.Equal(HttpMethod.Post, handler.Requests[0].Method);
        Assert.Equal(Base + $"shopping/lists/{List:D}/items", handler.Requests[0].Uri.AbsoluteUri);
        Assert.Contains(Item.ToString("D"), handler.Requests[0].Body, StringComparison.Ordinal);
        Assert.Equal("Milk", result.Value!.Text);
    }

    [Fact]
    public async Task ChangeItem_SendsTheFullStateWithBaseVersion_AndNoIfMatch()
    {
        var (client, handler) = Create();
        handler.EnqueueJson(HttpStatusCode.OK, $$"""{"item":{{ItemJson}},"outcome":"checked_wins"}""");

        var result = await client.ChangeItemAsync(
            Household, Item, new ChangeShoppingItemRequest(7, true, new QuantityDto(2, "l"), "ripe", "Milk", "dairy"), Ct);

        Assert.Equal(HttpMethod.Put, handler.Requests[0].Method);
        Assert.Equal(Base + $"shopping/items/{Item:D}", handler.Requests[0].Uri.AbsoluteUri);
        Assert.False(handler.Requests[0].Headers.ContainsKey("If-Match"));
        Assert.Contains("\"baseVersion\":7", handler.Requests[0].Body, StringComparison.Ordinal);
        Assert.Contains("\"checked\":true", handler.Requests[0].Body, StringComparison.Ordinal);
        Assert.Equal(ShoppingConflictOutcomeDto.CheckedWins, result.Value!.Outcome);
    }

    [Fact]
    public async Task DeleteItem_PassesTheBaseVersionAsQuery_AndReadsTheOutcome()
    {
        var (client, handler) = Create();
        handler.EnqueueJson(HttpStatusCode.OK, $$"""{"item":{{ItemJson}},"outcome":"both_deleted"}""");

        var result = await client.DeleteItemAsync(Household, Item, 7, Ct);

        Assert.Equal(HttpMethod.Delete, handler.Requests[0].Method);
        Assert.Equal(Base + $"shopping/items/{Item:D}?baseVersion=7", handler.Requests[0].Uri.AbsoluteUri);
        Assert.False(handler.Requests[0].Headers.ContainsKey("If-Match"));
        Assert.Equal(ShoppingConflictOutcomeDto.BothDeleted, result.Value!.Outcome);
    }

    [Fact]
    public async Task MoveItem_PostsTheTargetList()
    {
        var (client, handler) = Create();
        handler.EnqueueJson(HttpStatusCode.OK, ItemJson);

        var result = await client.MoveItemAsync(Household, Item, new MoveShoppingItemRequest(List), Ct);

        Assert.Equal(HttpMethod.Post, handler.Requests[0].Method);
        Assert.Equal(Base + $"shopping/items/{Item:D}/move", handler.Requests[0].Uri.AbsoluteUri);
        Assert.Contains(List.ToString("D"), handler.Requests[0].Body, StringComparison.Ordinal);
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task ChangeItem_KeepsTheRestrictedChangeError()
    {
        var (client, handler) = Create();
        handler.EnqueueJson(HttpStatusCode.Forbidden, """{"errorCode":"shopping.restricted_change"}""", "application/problem+json");

        var result = await client.ChangeItemAsync(
            Household, Item, new ChangeShoppingItemRequest(7, false, null, null, "Milk", "dairy"), Ct);

        Assert.False(result.IsSuccess);
        Assert.Equal(ShoppingErrorCodes.RestrictedChange, result.Error!.ErrorCode);
    }
}
