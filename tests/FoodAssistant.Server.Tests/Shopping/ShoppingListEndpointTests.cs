using System.Net;
using FoodAssistant.Contracts.Common;
using FoodAssistant.Contracts.Households;
using FoodAssistant.Contracts.Shopping;
using FoodAssistant.Contracts.Sync;
using FoodAssistant.Infrastructure.Shopping;
using FoodAssistant.Infrastructure.Sync;
using FoodAssistant.Server.Tests.Auth;
using FoodAssistant.Server.Tests.Support;

namespace FoodAssistant.Server.Tests.Shopping;

public sealed class ShoppingListEndpointTests(PostgresContainerFixture postgres)
{
    [Fact]
    public async Task GetLists_CreatesOneUnnamedDefaultList()
    {
        await using var api = await ShoppingApi.StartAsync(postgres);

        var first = await api.Owner.GetJsonAsync<List<ShoppingListDto>>($"{api.Base}/lists");
        var second = await api.Owner.GetJsonAsync<List<ShoppingListDto>>($"{api.Base}/lists");

        var list = Assert.Single(first);
        Assert.True(list.IsDefault);
        Assert.Equal(string.Empty, list.Name);
        Assert.Equal(list, Assert.Single(second));
    }

    [Fact]
    public async Task List_CreateUpdateDelete_WithETags()
    {
        await using var api = await ShoppingApi.StartAsync(postgres);
        var store = await HouseholdDataSeed.StoreAsync(api.App.Services, api.HouseholdId);

        using var create = await api.Owner.PostAsync($"{api.Base}/lists", new CreateShoppingListRequest(null, "  Market  ", store.Id));
        var created = await TestClient.ReadAsync<ShoppingListDto>(create, HttpStatusCode.Created);
        Assert.Equal(ShoppingApi.ETag(created.Version), create.Headers.ETag?.Tag);
        Assert.Equal(("Market", store.Id, false), (created.Name, created.StoreId, created.IsDefault));

        var url = $"{api.Base}/lists/{created.Id}";
        var change = new UpdateShoppingListRequest("Weekly", null, false, 5);
        using var missing = await ShoppingApi.SendAsync(api.Owner, HttpMethod.Put, url, change, etag: null);
        using var stale = await ShoppingApi.SendAsync(api.Owner, HttpMethod.Put, url, change, ShoppingApi.ETag(created.Version + 1));
        using var update = await ShoppingApi.SendAsync(api.Owner, HttpMethod.Put, url, change, ShoppingApi.ETag(created.Version));

        await TestClient.AssertProblemAsync(missing, HttpStatusCode.PreconditionRequired, ErrorCodes.PreconditionRequired);
        await TestClient.AssertProblemAsync(stale, HttpStatusCode.PreconditionFailed, ErrorCodes.PreconditionFailed);
        var updated = await TestClient.ReadAsync<ShoppingListDto>(update);
        Assert.Equal(("Weekly", (Guid?)null, 5), (updated.Name, updated.StoreId, updated.SortOrder));
        Assert.NotEqual(created.Version, updated.Version);

        using var delete = await ShoppingApi.SendAsync<object>(api.Owner, HttpMethod.Delete, url, null, ShoppingApi.ETag(updated.Version));
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        var lists = await api.Owner.GetJsonAsync<List<ShoppingListDto>>($"{api.Base}/lists");
        Assert.DoesNotContain(lists, l => l.Id == created.Id);
    }

    [Fact]
    public async Task MakeDefault_ResetsThePreviousDefault_AndDefaultCannotBeUnsetOrDeleted()
    {
        await using var api = await ShoppingApi.StartAsync(postgres);
        var previous = await api.DefaultListAsync();
        var other = await api.CreateListAsync("Market");

        using (var makeDefault = await ShoppingApi.SendAsync(
                   api.Owner,
                   HttpMethod.Put,
                   $"{api.Base}/lists/{other.Id}",
                   new UpdateShoppingListRequest(other.Name, null, true, other.SortOrder),
                   "*"))
        {
            Assert.True((await TestClient.ReadAsync<ShoppingListDto>(makeDefault)).IsDefault);
        }

        var lists = await api.Owner.GetJsonAsync<List<ShoppingListDto>>($"{api.Base}/lists");
        Assert.Equal(other.Id, Assert.Single(lists, l => l.IsDefault).Id);
        Assert.Contains(lists, l => l.Id == previous.Id && !l.IsDefault);

        using var unset = await ShoppingApi.SendAsync(
            api.Owner, HttpMethod.Put, $"{api.Base}/lists/{other.Id}", new UpdateShoppingListRequest("x", null, false, 0), "*");
        using var delete = await ShoppingApi.SendAsync<object>(api.Owner, HttpMethod.Delete, $"{api.Base}/lists/{other.Id}", null, "*");

        await TestClient.AssertProblemAsync(unset, HttpStatusCode.Conflict, ShoppingErrorCodes.DefaultList);
        await TestClient.AssertProblemAsync(delete, HttpStatusCode.Conflict, ShoppingErrorCodes.DefaultList);
    }

    [Fact]
    public async Task DeleteList_RemovesItsEntriesThroughTheChangeLog()
    {
        await using var api = await ShoppingApi.StartAsync(postgres);
        var list = await api.CreateListAsync("Market");
        var item = await api.AddTextAsync(list.Id, "Bread");
        var removed = await api.AddTextAsync(list.Id, "Butter");
        await api.DeleteOkAsync(api.Owner, removed.Id, removed.Version);

        using var delete = await ShoppingApi.SendAsync<object>(api.Owner, HttpMethod.Delete, $"{api.Base}/lists/{list.Id}", null, "*");

        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        Assert.Equal(0, await api.CountAsync<ShoppingItem>(i => i.ListId == list.Id));
        // The open entry gets its deletion recorded; the tombstone one more (after its soft delete).
        Assert.Equal(1, await DeletedEntriesAsync(api, item.Id));
        Assert.Equal(2, await DeletedEntriesAsync(api, removed.Id));
    }

    private static Task<int> DeletedEntriesAsync(ShoppingApi api, Guid itemId) =>
        api.CountAsync<ChangeLogEntry>(e => e.EntityId == itemId && e.EntityType == SyncEntityTypes.ShoppingItem && e.Deleted);

    [Fact]
    public async Task DeleteList_WhileEntriesAreAdded_NeitherFailsNorLeavesEntriesBehind()
    {
        await using var api = await ShoppingApi.StartAsync(postgres);
        var list = await api.CreateListAsync("Market");
        var url = $"{api.Base}/lists/{list.Id}/items";

        var adds = Enumerable.Range(0, 8).Select(async i =>
        {
            using var response = await api.Owner.PostAsync(url, new CreateShoppingItemRequest(null, null, $"Item {i}", null, null, null));
            return response.StatusCode;
        }).ToList();
        using var delete = await ShoppingApi.SendAsync<object>(api.Owner, HttpMethod.Delete, $"{api.Base}/lists/{list.Id}", null, "*");
        var statuses = await Task.WhenAll(adds);

        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        Assert.All(statuses, s => Assert.Contains(s, new[] { HttpStatusCode.Created, HttpStatusCode.NotFound }));
        Assert.Equal(0, await api.CountAsync<ShoppingItem>(i => i.ListId == list.Id));
    }

    [Fact]
    public async Task CreateList_InvalidInput_Returns400WithFieldErrors()
    {
        await using var api = await ShoppingApi.StartAsync(postgres);

        using var response = await api.Owner.PostAsync(
            $"{api.Base}/lists",
            new CreateShoppingListRequest(null, new string('x', 81), Guid.CreateVersion7()));

        var problem = await Api.ProblemAssert.IsProblemAsync(response, HttpStatusCode.BadRequest, ShoppingErrorCodes.ValidationFailed);
        Assert.Equal("too_long", problem.GetProperty("errors").GetProperty("name")[0].GetString());
        Assert.Equal("not_found", problem.GetProperty("errors").GetProperty("storeId")[0].GetString());
    }

    [Fact]
    public async Task Restricted_ReadsLists_ButCannotChangeThem()
    {
        await using var api = await ShoppingApi.StartAsync(postgres);
        var list = await api.DefaultListAsync();
        var (kid, _) = await api.AddMemberAsync(HouseholdRole.Restricted, "kid");
        using var _ = kid;

        var lists = await kid.GetJsonAsync<List<ShoppingListDto>>($"{api.Base}/lists");
        using var create = await kid.PostAsync($"{api.Base}/lists", new CreateShoppingListRequest(null, "Mine", null));
        using var update = await ShoppingApi.SendAsync(
            kid, HttpMethod.Put, $"{api.Base}/lists/{list.Id}", new UpdateShoppingListRequest("Mine", null, true, 0), "*");

        Assert.Single(lists);
        await TestClient.AssertProblemAsync(create, HttpStatusCode.Forbidden, HouseholdErrorCodes.Forbidden);
        await TestClient.AssertProblemAsync(update, HttpStatusCode.Forbidden, HouseholdErrorCodes.Forbidden);
    }

    [Fact]
    public async Task ForeignHousehold_Returns404()
    {
        await using var api = await ShoppingApi.StartAsync(postgres);
        var list = await api.CreateListAsync("Market");
        var foreign = await Scenario.CreateHouseholdAsync(api.Owner, "Neighbours");
        var (outsider, _) = await Scenario.AddMemberAsync(api.App, api.Owner, foreign, HouseholdRole.Owner, "outsider");
        using var _ = outsider;
        var ownRoot = ShoppingApi.Root(foreign);

        using var foreignLists = await outsider.GetAsync($"{api.Base}/lists");
        using var viaOwnUpdate = await ShoppingApi.SendAsync(
            outsider, HttpMethod.Put, $"{ownRoot}/lists/{list.Id}", new UpdateShoppingListRequest("Mine", null, false, 0), "*");
        using var viaOwnDelete = await ShoppingApi.SendAsync<object>(outsider, HttpMethod.Delete, $"{ownRoot}/lists/{list.Id}", null, "*");
        using var viaOwnItems = await outsider.GetAsync($"{ownRoot}/lists/{list.Id}/items");

        await TestClient.AssertProblemAsync(foreignLists, HttpStatusCode.NotFound, HouseholdErrorCodes.NotFound);
        await TestClient.AssertProblemAsync(viaOwnUpdate, HttpStatusCode.NotFound, ShoppingErrorCodes.ListNotFound);
        await TestClient.AssertProblemAsync(viaOwnDelete, HttpStatusCode.NotFound, ShoppingErrorCodes.ListNotFound);
        await TestClient.AssertProblemAsync(viaOwnItems, HttpStatusCode.NotFound, ShoppingErrorCodes.ListNotFound);
        Assert.Contains(await api.Owner.GetJsonAsync<List<ShoppingListDto>>($"{api.Base}/lists"), l => l.Id == list.Id);
    }
}
