using System.Net;
using FoodAssistant.Contracts.Common;
using FoodAssistant.Contracts.Households;
using FoodAssistant.Contracts.Shopping;
using FoodAssistant.Contracts.Stores;
using FoodAssistant.Contracts.Sync;
using FoodAssistant.Domain.Shopping;
using FoodAssistant.Infrastructure.Sync;
using FoodAssistant.Server.Tests.Api;
using FoodAssistant.Server.Tests.Auth;
using FoodAssistant.Server.Tests.Shopping;

namespace FoodAssistant.Server.Tests.Stores;

public sealed class StoreEndpointTests(PostgresContainerFixture postgres)
{
    [Fact]
    public async Task Store_CreateRenameDelete_WithETags()
    {
        await using var api = await ShoppingApi.StartAsync(postgres);
        var stores = $"/api/v1/households/{api.HouseholdId}/stores";

        using var create = await api.Owner.PostAsync(stores, new StoreRequest(null, "  Market "));
        var created = await TestClient.ReadAsync<StoreDto>(create, HttpStatusCode.Created);
        var url = $"{stores}/{created.Id}";
        using var missing = await ShoppingApi.SendAsync(api.Owner, HttpMethod.Put, url, new StoreRequest(null, "Corner shop"), etag: null);
        using var stale = await ShoppingApi.SendAsync(
            api.Owner, HttpMethod.Put, url, new StoreRequest(null, "Corner shop"), ShoppingApi.ETag(created.Version + 1));
        using var rename = await ShoppingApi.SendAsync(
            api.Owner, HttpMethod.Put, url, new StoreRequest(null, "Corner shop"), ShoppingApi.ETag(created.Version));

        Assert.Equal("Market", created.Name);
        Assert.Equal(ShoppingApi.ETag(created.Version), create.Headers.ETag?.Tag);
        await TestClient.AssertProblemAsync(missing, HttpStatusCode.PreconditionRequired, ErrorCodes.PreconditionRequired);
        await TestClient.AssertProblemAsync(stale, HttpStatusCode.PreconditionFailed, ErrorCodes.PreconditionFailed);
        var renamed = await TestClient.ReadAsync<StoreDto>(rename);
        Assert.Equal("Corner shop", Assert.Single(await api.Owner.GetJsonAsync<List<StoreDto>>(stores)).Name);

        using var delete = await ShoppingApi.SendAsync<object>(api.Owner, HttpMethod.Delete, url, null, ShoppingApi.ETag(renamed.Version));
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        Assert.Empty(await api.Owner.GetJsonAsync<List<StoreDto>>(stores));
    }

    [Fact]
    public async Task DeleteStore_DetachesItsListsThroughTheChangeLog()
    {
        await using var api = await ShoppingApi.StartAsync(postgres);
        var store = await CreateStoreAsync(api, "Market");
        using var createList = await api.Owner.PostAsync($"{api.Base}/lists", new CreateShoppingListRequest(null, "Weekly", store.Id));
        var list = await TestClient.ReadAsync<ShoppingListDto>(createList, HttpStatusCode.Created);

        using var delete = await ShoppingApi.SendAsync<object>(
            api.Owner, HttpMethod.Delete, $"/api/v1/households/{api.HouseholdId}/stores/{store.Id}", null, "*");

        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        var lists = await api.Owner.GetJsonAsync<List<ShoppingListDto>>($"{api.Base}/lists");
        Assert.Null(Assert.Single(lists, l => l.Id == list.Id).StoreId);
        Assert.Equal(2, await api.CountAsync<ChangeLogEntry>(
            e => e.EntityId == list.Id && e.EntityType == SyncEntityTypes.ShoppingList && !e.Deleted));
    }

    [Fact]
    public async Task InvalidInput_Returns400WithFieldErrors()
    {
        await using var api = await ShoppingApi.StartAsync(postgres);
        var store = await CreateStoreAsync(api, "Market");
        var order = $"/api/v1/households/{api.HouseholdId}/stores/{store.Id}/section-order";

        using var blank = await api.Owner.PostAsync($"/api/v1/households/{api.HouseholdId}/stores", new StoreRequest(null, " "));
        using var unknown = await api.Owner.PutAsync(order, new SetSectionOrderRequest(["produce", "aisle_7"]));
        using var duplicate = await api.Owner.PutAsync(order, new SetSectionOrderRequest(["produce", "produce"]));

        var blankProblem = await ProblemAssert.IsProblemAsync(blank, HttpStatusCode.BadRequest, StoreErrorCodes.ValidationFailed);
        var unknownProblem = await ProblemAssert.IsProblemAsync(unknown, HttpStatusCode.BadRequest, StoreErrorCodes.ValidationFailed);
        var duplicateProblem = await ProblemAssert.IsProblemAsync(duplicate, HttpStatusCode.BadRequest, StoreErrorCodes.ValidationFailed);
        Assert.Equal("required", blankProblem.GetProperty("errors").GetProperty("name")[0].GetString());
        Assert.Equal("unknown", unknownProblem.GetProperty("errors").GetProperty("sections")[0].GetString());
        Assert.Equal("duplicate", duplicateProblem.GetProperty("errors").GetProperty("sections")[0].GetString());
    }

    [Fact]
    public async Task DefaultSections_AreTheFixedListInDefaultOrder()
    {
        await using var api = await ShoppingApi.StartAsync(postgres);

        var order = await api.Owner.GetJsonAsync<SectionOrderDto>($"{api.Base}/sections");

        Assert.Equal((null, false), (order.StoreId, order.HasManualOrder));
        Assert.Equal(StoreSections.All, order.Sections);
    }

    [Fact]
    public async Task Restricted_ReadsStoresAndOrder_ButCannotChangeThem()
    {
        await using var api = await ShoppingApi.StartAsync(postgres);
        var store = await CreateStoreAsync(api, "Market");
        var (kid, _) = await api.AddMemberAsync(HouseholdRole.Restricted, "kid");
        using var _ = kid;
        var stores = $"/api/v1/households/{api.HouseholdId}/stores";

        Assert.Single(await kid.GetJsonAsync<List<StoreDto>>(stores));
        Assert.Equal(StoreSections.All, (await kid.GetJsonAsync<SectionOrderDto>($"{stores}/{store.Id}/section-order")).Sections);
        using var create = await kid.PostAsync(stores, new StoreRequest(null, "Mine"));
        using var order = await kid.PutAsync($"{stores}/{store.Id}/section-order", new SetSectionOrderRequest(["frozen"]));
        using var reset = await kid.DeleteAsync($"{stores}/{store.Id}/section-order");

        await TestClient.AssertProblemAsync(create, HttpStatusCode.Forbidden, HouseholdErrorCodes.Forbidden);
        await TestClient.AssertProblemAsync(order, HttpStatusCode.Forbidden, HouseholdErrorCodes.Forbidden);
        await TestClient.AssertProblemAsync(reset, HttpStatusCode.Forbidden, HouseholdErrorCodes.Forbidden);
    }

    [Fact]
    public async Task ForeignHousehold_StoresAreNotFound()
    {
        await using var api = await ShoppingApi.StartAsync(postgres);
        var store = await CreateStoreAsync(api, "Market");
        var foreign = await Scenario.CreateHouseholdAsync(api.Owner, "Neighbours");
        var (outsider, _) = await Scenario.AddMemberAsync(api.App, api.Owner, foreign, HouseholdRole.Owner, "outsider");
        using var _ = outsider;
        var ownStores = $"/api/v1/households/{foreign}/stores";

        using var direct = await outsider.GetAsync($"/api/v1/households/{api.HouseholdId}/stores");
        using var rename = await ShoppingApi.SendAsync(outsider, HttpMethod.Put, $"{ownStores}/{store.Id}", new StoreRequest(null, "Mine"), "*");
        using var delete = await ShoppingApi.SendAsync<object>(outsider, HttpMethod.Delete, $"{ownStores}/{store.Id}", null, "*");
        using var order = await outsider.GetAsync($"{ownStores}/{store.Id}/section-order");
        using var setOrder = await outsider.PutAsync($"{ownStores}/{store.Id}/section-order", new SetSectionOrderRequest(["frozen"]));
        using var list = await outsider.PostAsync(
            $"{ShoppingApi.Root(foreign)}/lists", new CreateShoppingListRequest(null, "Mine", store.Id));

        await TestClient.AssertProblemAsync(direct, HttpStatusCode.NotFound, HouseholdErrorCodes.NotFound);
        await TestClient.AssertProblemAsync(rename, HttpStatusCode.NotFound, StoreErrorCodes.StoreNotFound);
        await TestClient.AssertProblemAsync(delete, HttpStatusCode.NotFound, StoreErrorCodes.StoreNotFound);
        await TestClient.AssertProblemAsync(order, HttpStatusCode.NotFound, StoreErrorCodes.StoreNotFound);
        await TestClient.AssertProblemAsync(setOrder, HttpStatusCode.NotFound, StoreErrorCodes.StoreNotFound);
        var problem = await ProblemAssert.IsProblemAsync(list, HttpStatusCode.BadRequest, ShoppingErrorCodes.ValidationFailed);
        Assert.Equal("not_found", problem.GetProperty("errors").GetProperty("storeId")[0].GetString());
    }

    internal static async Task<StoreDto> CreateStoreAsync(ShoppingApi api, string name)
    {
        using var response = await api.Owner.PostAsync($"/api/v1/households/{api.HouseholdId}/stores", new StoreRequest(null, name));
        return await TestClient.ReadAsync<StoreDto>(response, HttpStatusCode.Created);
    }
}
