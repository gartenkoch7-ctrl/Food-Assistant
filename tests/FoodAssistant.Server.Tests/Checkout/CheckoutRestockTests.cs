using FoodAssistant.Contracts.Catalog;
using FoodAssistant.Contracts.Common;
using FoodAssistant.Contracts.Inventory;
using FoodAssistant.Contracts.Shopping;
using FoodAssistant.Server.Tests.Auth;
using FoodAssistant.Server.Tests.Catalog;
using FoodAssistant.Server.Tests.Inventory;
using static FoodAssistant.Server.Tests.Checkout.CheckoutApi;

namespace FoodAssistant.Server.Tests.Checkout;

/// <summary>Checkout and minimum stock (F with G): buying the product re-arms the minimum-stock check.</summary>
public sealed class CheckoutRestockTests(PostgresContainerFixture postgres)
{
    [Fact]
    public async Task CompletingTheAutomaticEntry_ReArmsTheMinimum_SoTheNextDropAddsANewEntry()
    {
        await using var app = await InventoryApp.StartAsync(postgres);
        var listId = await DefaultListAsync(app);
        var batch = await InventoryApi.AddAsync(app, InventoryApi.Add(app.Milk.Id, app.Fridge.Id, amount: 3m));
        await SetMinimumAsync(app, 2m);
        await UseAsync(app, batch.Id, 2m);
        var automatic = Assert.Single(await ItemsAsync(app, listId));
        Assert.Equal(ShoppingItemOriginDto.MinimumStock, automatic.Origin);

        await SetCheckedAsync(app, automatic, true);
        var entry = Assert.Single((await ProposeAsync(app, listId)).Entries);
        var result = await CompleteAsync(
            app,
            listId,
            Book(entry) with { Quantity = new QuantityDto(4m, "l"), StorageLocationId = app.Fridge.Id });
        Assert.Equal(1, result.BatchesCreated);
        Assert.Empty(await ItemsAsync(app, listId));

        // 1 l + 4 l bought = 5 l; using 3.5 l leaves 1.5 l, below the 2 l minimum again.
        await UseAsync(app, Assert.Single(result.BatchIds), 3.5m);

        var next = Assert.Single(await ItemsAsync(app, listId));
        Assert.NotEqual(automatic.Id, next.Id);
        Assert.Equal((ShoppingItemOriginDto.MinimumStock, new QuantityDto(0.5m, "l")), (next.Origin, next.Quantity));
    }

    private static Task<List<ShoppingItemDto>> ItemsAsync(InventoryApp app, Guid listId) =>
        app.Owner.GetJsonAsync<List<ShoppingItemDto>>($"{ShoppingUrl(app)}/lists/{listId}/items?includeChecked=true");

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
        using var response = await InventoryApi.SendAsync(
            app.Owner,
            HttpMethod.Post,
            $"{app.Inventory}/batches/{batchId}/consume",
            new ConsumeBatchRequest(ConsumptionKindDto.Partial, new QuantityDto(litres, "l"), null));
        Assert.True(response.IsSuccessStatusCode);
    }
}
