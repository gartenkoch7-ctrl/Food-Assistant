using FoodAssistant.Contracts.Catalog;
using FoodAssistant.Contracts.Common;
using FoodAssistant.Contracts.Inventory;
using FoodAssistant.Contracts.Shopping;
using FoodAssistant.Server.Tests.Auth;
using FoodAssistant.Server.Tests.Catalog;
using FoodAssistant.Server.Tests.Inventory;
using FoodAssistant.Server.Tests.Shopping;
using static FoodAssistant.Server.Tests.Inventory.InventoryApi;

namespace FoodAssistant.Server.Tests.Restock;

/// <summary>Minimum stock through the API: stock changes put a product on the default list once.</summary>
public sealed class MinimumStockHandlerTests(PostgresContainerFixture postgres)
{
    [Fact]
    public async Task FallingBelowMinimum_AddsOneAutomaticEntry_NotASecondOne()
    {
        await using var app = await InventoryApp.StartAsync(postgres);
        var batch = await AddAsync(app, Add(app.Milk.Id, app.Fridge.Id, amount: 3m));
        await SetMinimumAsync(app, 2m);
        Assert.Empty(await DefaultListItemsAsync(app));

        await UseAsync(app, batch.Id, 2m);
        await UseAsync(app, batch.Id, 0.25m);

        var entry = Assert.Single(await DefaultListItemsAsync(app));
        Assert.Equal(("Milk", ShoppingItemOriginDto.MinimumStock, app.Milk.Id), (entry.Text, entry.Origin, entry.ProductId));
        Assert.Equal(new QuantityDto(1m, "l"), entry.Quantity);
    }

    [Fact]
    public async Task DeletedEntry_ComesBackOnlyAfterTheStockWasSufficientAgain()
    {
        await using var app = await InventoryApp.StartAsync(postgres);
        var batch = await AddAsync(app, Add(app.Milk.Id, app.Fridge.Id, amount: 3m));
        await SetMinimumAsync(app, 2m);
        await UseAsync(app, batch.Id, 2m);
        var first = Assert.Single(await DefaultListItemsAsync(app));
        using (var delete = await app.Owner.DeleteAsync($"{ShoppingBase(app)}/items/{first.Id}?baseVersion={first.Version}"))
        {
            Assert.True(delete.IsSuccessStatusCode);
        }

        await UseAsync(app, batch.Id, 0.5m);
        Assert.Empty(await DefaultListItemsAsync(app));

        // "Shopping done": enough stock again re-arms the check; the next drop adds a new entry.
        var bought = await AddAsync(app, Add(app.Milk.Id, app.Fridge.Id, amount: 4m));
        await UseAsync(app, bought.Id, 3m);

        var second = Assert.Single(await DefaultListItemsAsync(app));
        Assert.NotEqual(first.Id, second.Id);
        Assert.Equal(new QuantityDto(0.5m, "l"), second.Quantity);
    }

    [Fact]
    public async Task ProductAlreadyOnTheList_GetsNoSecondEntry()
    {
        await using var app = await InventoryApp.StartAsync(postgres);
        var batch = await AddAsync(app, Add(app.Milk.Id, app.Fridge.Id, amount: 3m));
        await SetMinimumAsync(app, 2m);
        var list = await DefaultListAsync(app);
        using (var manual = await app.Owner.PostAsync(
                   $"{ShoppingBase(app)}/lists/{list.Id}/items",
                   new CreateShoppingItemRequest(null, app.Milk.Id, null, null, null, null)))
        {
            Assert.True(manual.IsSuccessStatusCode);
        }

        await UseAsync(app, batch.Id, 2.5m);

        var entry = Assert.Single(await DefaultListItemsAsync(app));
        Assert.Equal(ShoppingItemOriginDto.Manual, entry.Origin);
    }

    [Fact]
    public async Task RaisingTheMinimumAboveTheStock_AddsTheEntryRightAway()
    {
        await using var app = await InventoryApp.StartAsync(postgres);
        await AddAsync(app, Add(app.Milk.Id, app.Fridge.Id, amount: 1m));

        await SetMinimumAsync(app, 3m);

        var entry = Assert.Single(await DefaultListItemsAsync(app));
        Assert.Equal(new QuantityDto(2m, "l"), entry.Quantity);
    }

    [Fact]
    public async Task ProductResponses_CarryTheVersionAfterTheHandlerChangedTheFlag()
    {
        await using var app = await InventoryApp.StartAsync(postgres);
        var created = await CatalogApi.CreateProductAsync(
            app.Owner,
            app.HouseholdId,
            CatalogApi.NewProduct("Juice") with { MinimumStock = new QuantityDto(2m, "l") });
        var url = $"{CatalogApi.Products(app.HouseholdId)}/{created.Id}";

        // The handler flipped the flag during the POST; the returned version must still be current.
        using var update = await CatalogApi.SendAsync(
            app.Owner, HttpMethod.Put, url,
            CatalogApi.Update("Juice") with { MinimumStock = new QuantityDto(3m, "l") },
            CatalogApi.ETag(created.Version));
        var updated = await TestClient.ReadAsync<ProductDto>(update);
        using var again = await CatalogApi.SendAsync(
            app.Owner, HttpMethod.Put, url,
            CatalogApi.Update("Juice") with { MinimumStock = new QuantityDto(3m, "l") },
            CatalogApi.ETag(updated.Version));

        Assert.True(again.IsSuccessStatusCode);
    }

    [Fact]
    public async Task RemovingTheMinimum_ForgetsTheTrigger_SoANewMinimumAddsAgain()
    {
        await using var app = await InventoryApp.StartAsync(postgres);
        await SetMinimumAsync(app, 2m);
        var first = Assert.Single(await DefaultListItemsAsync(app));
        using (var delete = await app.Owner.DeleteAsync($"{ShoppingBase(app)}/items/{first.Id}?baseVersion={first.Version}"))
        {
            Assert.True(delete.IsSuccessStatusCode);
        }

        var url = $"{CatalogApi.Products(app.HouseholdId)}/{app.Milk.Id}";
        var product = await app.Owner.GetJsonAsync<ProductDto>(url);
        using (var remove = await CatalogApi.SendAsync(
                   app.Owner, HttpMethod.Put, url, CatalogApi.Update("Milk"), CatalogApi.ETag(product.Version)))
        {
            Assert.True(remove.IsSuccessStatusCode);
        }

        await SetMinimumAsync(app, 2m);

        Assert.Single(await DefaultListItemsAsync(app));
    }

    private static string ShoppingBase(InventoryApp app) => ShoppingApi.Root(app.HouseholdId);

    private static async Task<ShoppingListDto> DefaultListAsync(InventoryApp app)
    {
        var lists = await app.Owner.GetJsonAsync<List<ShoppingListDto>>($"{ShoppingBase(app)}/lists");
        return Assert.Single(lists, l => l.IsDefault);
    }

    private static async Task<List<ShoppingItemDto>> DefaultListItemsAsync(InventoryApp app)
    {
        var list = await DefaultListAsync(app);
        return await app.Owner.GetJsonAsync<List<ShoppingItemDto>>($"{ShoppingBase(app)}/lists/{list.Id}/items");
    }

    private static async Task SetMinimumAsync(InventoryApp app, decimal litres)
    {
        var url = $"{CatalogApi.Products(app.HouseholdId)}/{app.Milk.Id}";
        var product = await app.Owner.GetJsonAsync<ProductDto>(url);
        using var response = await CatalogApi.SendAsync(
            app.Owner,
            HttpMethod.Put,
            url,
            CatalogApi.Update("Milk") with { MinimumStock = new QuantityDto(litres, "l") },
            CatalogApi.ETag(product.Version));
        Assert.True(response.IsSuccessStatusCode);
    }

    private static async Task UseAsync(InventoryApp app, Guid batchId, decimal litres)
    {
        using var response = await SendAsync(
            app.Owner,
            HttpMethod.Post,
            $"{app.Inventory}/batches/{batchId}/consume",
            new ConsumeBatchRequest(ConsumptionKindDto.Partial, new QuantityDto(litres, "l"), null));
        Assert.True(response.IsSuccessStatusCode);
    }
}
