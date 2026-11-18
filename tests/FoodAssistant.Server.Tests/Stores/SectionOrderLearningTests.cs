using System.Net;
using FoodAssistant.Contracts.Shopping;
using FoodAssistant.Contracts.Stores;
using FoodAssistant.Domain.Shopping;
using FoodAssistant.Server.Tests.Auth;
using FoodAssistant.Server.Tests.Shopping;

namespace FoodAssistant.Server.Tests.Stores;

/// <summary>The learned section order through the shopping API: checking entries off in a store.</summary>
public sealed class SectionOrderLearningTests(PostgresContainerFixture postgres)
{
    // Reverse of the default order, so learning visibly changes it.
    private static readonly string[] Trip = [StoreSections.Beverages, StoreSections.DairyChilled, StoreSections.Produce];

    [Fact]
    public async Task CheckOffsInOneTrip_ChangeTheOrderOfThatStore()
    {
        await using var api = await ShoppingApi.StartAsync(postgres);
        var (store, listId) = await StoreWithListAsync(api);

        await CheckOffAsync(api, listId, Trip, pause: TimeSpan.FromMinutes(3));

        var order = await OrderAsync(api, store.Id);
        Assert.False(order.HasManualOrder);
        Assert.Equal(StoreSections.Beverages, order.Sections[0]);
        Assert.Equal(StoreSections.Produce, order.Sections[^1]);
        Assert.True(IndexOf(order, StoreSections.Beverages) < IndexOf(order, StoreSections.DairyChilled));
    }

    [Fact]
    public async Task PauseOverTwoHours_StartsANewTrip()
    {
        await using var api = await ShoppingApi.StartAsync(postgres);
        var (store, listId) = await StoreWithListAsync(api);

        await CheckOffAsync(api, listId, Trip, pause: TimeSpan.FromHours(2) + TimeSpan.FromMinutes(1));

        Assert.Equal(StoreSections.All, (await OrderAsync(api, store.Id)).Sections);
    }

    [Fact]
    public async Task ManualOrder_WinsOverLearned_AndResetReturnsToLearned()
    {
        await using var api = await ShoppingApi.StartAsync(postgres);
        var (store, listId) = await StoreWithListAsync(api);
        await CheckOffAsync(api, listId, Trip, pause: TimeSpan.FromMinutes(1));
        var url = $"/api/v1/households/{api.HouseholdId}/stores/{store.Id}/section-order";

        using var set = await api.Owner.PutAsync(url, new SetSectionOrderRequest([StoreSections.Frozen, StoreSections.Produce]));
        var manual = await TestClient.ReadAsync<SectionOrderDto>(set);
        using var reset = await api.Owner.DeleteAsync(url);
        var learned = await TestClient.ReadAsync<SectionOrderDto>(reset);

        Assert.True(manual.HasManualOrder);
        Assert.Equal([StoreSections.Frozen, StoreSections.Produce, StoreSections.Beverages], manual.Sections.Take(3));
        Assert.Equal(StoreSections.All.Count, manual.Sections.Count);
        Assert.False(learned.HasManualOrder);
        Assert.Equal(StoreSections.Beverages, learned.Sections[0]);
    }

    [Fact]
    public async Task CheckOffOnListWithoutStore_LearnsNothing()
    {
        await using var api = await ShoppingApi.StartAsync(postgres);
        var (store, _) = await StoreWithListAsync(api);
        var plainList = await api.CreateListAsync("No store");

        await CheckOffAsync(api, plainList.Id, Trip, pause: TimeSpan.FromMinutes(1));

        Assert.Equal(StoreSections.All, (await OrderAsync(api, store.Id)).Sections);
    }

    private static async Task<(StoreDto Store, Guid ListId)> StoreWithListAsync(ShoppingApi api)
    {
        var store = await StoreEndpointTests.CreateStoreAsync(api, "Market");
        using var response = await api.Owner.PostAsync($"{api.Base}/lists", new CreateShoppingListRequest(null, "Market", store.Id));
        return (store, (await TestClient.ReadAsync<ShoppingListDto>(response, HttpStatusCode.Created)).Id);
    }

    // Adds one entry per section and checks them off in the given order, with a pause between.
    private static async Task CheckOffAsync(ShoppingApi api, Guid listId, IEnumerable<string> sections, TimeSpan pause)
    {
        foreach (var section in sections)
        {
            var item = await api.AddTextAsync(listId, $"Item {section}", sectionKey: section);
            await api.ChangeOkAsync(api.Owner, item, c => c with { Checked = true });
            api.App.Time.Advance(pause);
        }
    }

    private static Task<SectionOrderDto> OrderAsync(ShoppingApi api, Guid storeId) =>
        api.Owner.GetJsonAsync<SectionOrderDto>($"/api/v1/households/{api.HouseholdId}/stores/{storeId}/section-order");

    private static int IndexOf(SectionOrderDto order, string section) => order.Sections.ToList().IndexOf(section);
}
