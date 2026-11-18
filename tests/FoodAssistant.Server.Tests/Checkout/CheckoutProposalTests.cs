using System.Net;
using FoodAssistant.Contracts.Checkout;
using FoodAssistant.Contracts.Common;
using FoodAssistant.Contracts.Households;
using FoodAssistant.Contracts.Inventory;
using FoodAssistant.Domain.Catalog;
using FoodAssistant.Domain.Expiry;
using FoodAssistant.Domain.Inventory;
using FoodAssistant.Server.Tests.Auth;
using FoodAssistant.Server.Tests.Inventory;
using FoodAssistant.Server.Tests.Support;
using static FoodAssistant.Server.Tests.Checkout.CheckoutApi;

namespace FoodAssistant.Server.Tests.Checkout;

/// <summary>Suggestions for completing a shopping list (F1): amount, location cascade, expiry date.</summary>
public sealed class CheckoutProposalTests(PostgresContainerFixture postgres)
{
    [Fact]
    public async Task Propose_OnlyCheckedEntries_WithPrefilledValues()
    {
        await using var app = await InventoryApp.StartAsync(postgres);
        app.Rules.Set(StorageType.Frozen, new ShelfLifeRule { DaysFrozen = 90 });
        var today = app.Today();
        var listId = await DefaultListAsync(app);
        var peas = await HouseholdDataSeed.ProductAsync(app.Services, app.HouseholdId, "Peas", ProductCategories.Frozen);
        var cheese = await HouseholdDataSeed.ProductAsync(app.Services, app.HouseholdId, "Cheese", ProductCategories.Cheese);
        var yogurt = await HouseholdDataSeed.ProductAsync(app.Services, app.HouseholdId, "Yogurt");
        await UpdateProductAsync(app, cheese.Id, p => p.DefaultStorageLocationId = app.Pantry.Id);
        await UpdateProductAsync(app, yogurt.Id, p => (p.LastShelfLifeDays, p.LastExpiryKind) = (10, ExpiryKind.BestBefore));
        await HouseholdDataSeed.BatchAsync(app.Services, app.HouseholdId, yogurt.Id, app.Pantry.Id);

        var milk = await AddItemAsync(app, listId, app.Milk.Id);
        var frozenPeas = await AddItemAsync(app, listId, peas.Id);
        var bread = await AddItemAsync(app, listId, text: "Bread", quantity: new QuantityDto(2m, "piece"));
        var pantryCheese = await AddItemAsync(app, listId, cheese.Id);
        var lastBought = await AddItemAsync(app, listId, yogurt.Id);
        await AddItemAsync(app, listId, text: "Not bought yet", check: false);

        var proposal = await ProposeAsync(app, listId);

        Assert.Equal(listId, proposal.ListId);
        Assert.Equal(new[] { milk.Id, frozenPeas.Id, bread.Id, pantryCheese.Id, lastBought.Id }, proposal.Entries.Select(e => e.ItemId));
        var entries = proposal.Entries.ToDictionary(e => e.ItemId);
        Assert.Equal(
            new CheckoutEntryDto(milk.Id, milk.Version, app.Milk.Id, "Milk", ProductCategories.Dairy, new QuantityDto(1m, "l"), app.Fridge.Id, null, ExpiryKindDto.None, false, false),
            entries[milk.Id]);
        Assert.Equal((app.Freezer.Id, today.AddDays(90), ExpiryKindDto.UseBy, true), Summary(entries[frozenPeas.Id]));
        Assert.Equal(
            new CheckoutEntryDto(bread.Id, bread.Version, null, "Bread", null, new QuantityDto(2m, "piece"), app.Freezer.Id, null, ExpiryKindDto.None, false, true),
            entries[bread.Id]);
        Assert.Equal((app.Pantry.Id, null, ExpiryKindDto.None, false), Summary(entries[pantryCheese.Id]));
        Assert.Equal((app.Pantry.Id, today.AddDays(10), ExpiryKindDto.BestBefore, false), Summary(entries[lastBought.Id]));
    }

    [Fact]
    public async Task Propose_StoredShelfLifeOutOfRange_FreezerStillGetsTheRule()
    {
        await using var app = await InventoryApp.StartAsync(postgres);
        app.Rules.Set(StorageType.Frozen, new ShelfLifeRule { DaysFrozen = 90 });
        var listId = await DefaultListAsync(app);
        var peas = await HouseholdDataSeed.ProductAsync(app.Services, app.HouseholdId, "Peas", ProductCategories.Frozen);
        await UpdateProductAsync(app, peas.Id, p => (p.LastShelfLifeDays, p.LastExpiryKind) = (99_999, ExpiryKind.BestBefore));
        await AddItemAsync(app, listId, peas.Id);

        var entry = Assert.Single((await ProposeAsync(app, listId)).Entries);

        Assert.Equal((app.Freezer.Id, app.Today().AddDays(90), ExpiryKindDto.UseBy, true), Summary(entry));
    }

    [Fact]
    public async Task Propose_NoLocations_NoSuggestedLocation_CompleteRequiresOne()
    {
        await using var app = await InventoryApp.StartAsync(postgres);
        var otherId = await Scenario.CreateHouseholdAsync(app.Owner, "Empty");
        var product = await HouseholdDataSeed.ProductAsync(app.Services, otherId, "Rice", ProductCategories.Grains);
        var list = await HouseholdDataSeed.ShoppingListAsync(app.Services, otherId);
        var item = await HouseholdDataSeed.ShoppingItemAsync(app.Services, otherId, list.Id, "Rice", product.Id);
        var url = $"/api/v1/households/{otherId}/shopping/items/{item.Id}";
        using (var check = await app.Owner.PutAsync(url, new Contracts.Shopping.ChangeShoppingItemRequest(1, true, null, null, "Rice", item.SectionKey)))
        {
            Assert.Equal(HttpStatusCode.OK, check.StatusCode);
        }

        var checkout = $"/api/v1/households/{otherId}/shopping/lists/{list.Id}/checkout";
        var entry = Assert.Single((await app.Owner.GetJsonAsync<CheckoutProposalDto>(checkout)).Entries);
        using var complete = await app.Owner.PostAsync(checkout, new CompleteCheckoutRequest([Book(entry)]));

        Assert.Null(entry.StorageLocationId);
        await TestClient.AssertProblemAsync(complete, HttpStatusCode.BadRequest, CheckoutErrorCodes.ValidationFailed);
        Assert.Contains("lines[0].storageLocationId", await complete.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Restricted_403_ForeignHousehold_404()
    {
        await using var app = await InventoryApp.StartAsync(postgres);
        var listId = await DefaultListAsync(app);
        var item = await AddItemAsync(app, listId, app.Milk.Id);
        using var kid = await app.AddMemberAsync(HouseholdRole.Restricted, "kid");
        var foreignId = await Scenario.CreateHouseholdAsync(app.Owner, "Neighbours");
        var foreignList = await HouseholdDataSeed.ShoppingListAsync(app.Services, foreignId);
        var foreignCheckout = $"/api/v1/households/{foreignId}/shopping/lists/{foreignList.Id}/checkout";

        var line = new CheckoutLineRequest(item.Id, item.Version, true, null, null, null, null, null, ExpiryKindDto.None, false);
        using var kidGet = await kid.GetAsync(CheckoutUrl(app, listId));
        using var kidPost = await kid.PostAsync(CheckoutUrl(app, listId), new CompleteCheckoutRequest([line]));
        using var viaOwnGet = await app.Owner.GetAsync(CheckoutUrl(app, foreignList.Id));
        using var viaOwnPost = await app.Owner.PostAsync(CheckoutUrl(app, foreignList.Id), new CompleteCheckoutRequest([line]));
        using var outsiderGet = await kid.GetAsync(foreignCheckout);
        using var outsiderPost = await kid.PostAsync(foreignCheckout, new CompleteCheckoutRequest([line]));

        Assert.Equal(HttpStatusCode.Forbidden, kidGet.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, kidPost.StatusCode);
        await TestClient.AssertProblemAsync(viaOwnGet, HttpStatusCode.NotFound, CheckoutErrorCodes.ListNotFound);
        await TestClient.AssertProblemAsync(viaOwnPost, HttpStatusCode.NotFound, CheckoutErrorCodes.ListNotFound);
        await TestClient.AssertProblemAsync(outsiderGet, HttpStatusCode.NotFound, HouseholdErrorCodes.NotFound);
        await TestClient.AssertProblemAsync(outsiderPost, HttpStatusCode.NotFound, HouseholdErrorCodes.NotFound);
        Assert.Single((await ProposeAsync(app, listId)).Entries);
    }

    private static (Guid? Location, DateOnly? Date, ExpiryKindDto Kind, bool FromRule) Summary(CheckoutEntryDto entry) =>
        (entry.StorageLocationId, entry.ExpiresOn, entry.ExpiryKind, entry.ExpiryFromRule);
}
