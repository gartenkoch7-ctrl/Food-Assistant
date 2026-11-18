using System.Net;
using FoodAssistant.Contracts.Catalog;
using FoodAssistant.Contracts.Common;
using FoodAssistant.Contracts.Households;
using FoodAssistant.Contracts.Inventory;
using FoodAssistant.Server.Tests.Auth;
using FoodAssistant.Server.Tests.Support;
using static FoodAssistant.Server.Tests.Inventory.InventoryApi;

namespace FoodAssistant.Server.Tests.Inventory;

/// <summary>Inventory overview: sorting, filters, cursor, status in the household's time zone.</summary>
public sealed class InventoryQueryTests(PostgresContainerFixture postgres)
{
    [Fact]
    public async Task List_SortsByNextExpiryThenName_PagesWithCursor()
    {
        await using var app = await InventoryApp.StartAsync(postgres);
        var today = app.Today();
        var butter = await HouseholdDataSeed.ProductAsync(app.Services, app.HouseholdId, "Butter");
        var apples = await HouseholdDataSeed.ProductAsync(app.Services, app.HouseholdId, "Apples");
        var cheese = await HouseholdDataSeed.ProductAsync(app.Services, app.HouseholdId, "Cheese");
        await AddAsync(app, Add(app.Milk.Id, app.Fridge.Id, date: today.AddDays(3)));
        await AddAsync(app, Add(butter.Id, app.Fridge.Id, date: today.AddDays(3)));
        await AddAsync(app, Add(apples.Id, app.Pantry.Id));
        await AddAsync(app, Add(cheese.Id, app.Fridge.Id, date: today.AddDays(1)));
        await AddAsync(app, Add(cheese.Id, app.Fridge.Id, date: today.AddDays(9)));

        var names = new List<string>();
        string? cursor = null;
        do
        {
            var url = $"{app.Inventory}?limit=2" + (cursor is null ? string.Empty : $"&cursor={cursor}");
            var page = await app.Owner.GetJsonAsync<CursorPage<InventoryProductDto>>(url);
            names.AddRange(page.Items.Select(p => p.Product.Name));
            cursor = page.NextCursor;
        }
        while (cursor is not null);

        Assert.Equal(["Cheese", "Butter", "Milk", "Apples"], names);
    }

    [Fact]
    public async Task List_StatusUsesHouseholdTodayAndLeadTime_FiltersBatches()
    {
        await using var app = await InventoryApp.StartAsync(postgres);
        var today = app.Today();
        var bread = await HouseholdDataSeed.ProductAsync(app.Services, app.HouseholdId, "Bread");
        await HouseholdDataSeed.BatchAsync(app.Services, app.HouseholdId, app.Milk.Id, app.Fridge.Id, today.AddDays(-1));
        await HouseholdDataSeed.BatchAsync(app.Services, app.HouseholdId, app.Milk.Id, app.Fridge.Id, today.AddDays(2));
        await HouseholdDataSeed.BatchAsync(app.Services, app.HouseholdId, bread.Id, app.Fridge.Id, today.AddDays(3));

        var all = await app.Owner.GetJsonAsync<CursorPage<InventoryProductDto>>(app.Inventory);
        var expired = await app.Owner.GetJsonAsync<CursorPage<InventoryProductDto>>($"{app.Inventory}?status=expired");
        var soon = await app.Owner.GetJsonAsync<CursorPage<InventoryProductDto>>($"{app.Inventory}?status=soon");

        var milk = all.Items.Single(p => p.Product.Id == app.Milk.Id);
        Assert.Equal((ExpiryStatusDto.Expired, today.AddDays(-1), 2), (milk.WorstStatus, milk.NextExpiry, milk.Batches.Count));
        Assert.Equal([ExpiryStatusDto.Expired, ExpiryStatusDto.Soon], milk.Batches.Select(b => b.Status));
        Assert.Equal(ExpiryStatusDto.Ok, all.Items.Single(p => p.Product.Id == bread.Id).WorstStatus);
        var onlyExpired = Assert.Single(expired.Items);
        Assert.Equal((app.Milk.Id, -1), (onlyExpired.Product.Id, Assert.Single(onlyExpired.Batches).DaysUntilExpiry));
        Assert.Equal([2], Assert.Single(soon.Items).Batches.Select(b => b.DaysUntilExpiry!.Value));
    }

    [Fact]
    public async Task List_FiltersLocationAndName_SumsConvertibleUnits()
    {
        await using var app = await InventoryApp.StartAsync(postgres);
        var rice = await HouseholdDataSeed.ProductAsync(app.Services, app.HouseholdId, "Rice");
        await AddAsync(app, Add(app.Milk.Id, app.Fridge.Id, amount: 1m, unit: "l"));
        await AddAsync(app, Add(app.Milk.Id, app.Fridge.Id, amount: 500m, unit: "ml"));
        await AddAsync(app, Add(app.Milk.Id, app.Fridge.Id, amount: 2m, unit: "package"));
        await AddAsync(app, Add(rice.Id, app.Pantry.Id, amount: 1m, unit: "kg"));

        var fridge = await app.Owner.GetJsonAsync<CursorPage<InventoryProductDto>>($"{app.Inventory}?locationId={app.Fridge.Id}");
        var byName = await app.Owner.GetJsonAsync<CursorPage<InventoryProductDto>>($"{app.Inventory}?query=RI");

        var milk = Assert.Single(fridge.Items);
        Assert.Equal([new QuantityDto(1.5m, "l"), new QuantityDto(2m, "package")], milk.Totals.OrderBy(t => t.Unit, StringComparer.Ordinal));
        Assert.Equal(ExpiryStatusDto.NoDate, milk.WorstStatus);
        Assert.Equal(rice.Id, Assert.Single(byName.Items).Product.Id);
    }

    [Fact]
    public async Task List_InvalidStatusOrCursor_400()
    {
        await using var app = await InventoryApp.StartAsync(postgres);

        using var status = await app.Owner.GetAsync($"{app.Inventory}?status=later");
        using var cursor = await app.Owner.GetAsync($"{app.Inventory}?cursor=not-a-cursor");

        await TestClient.AssertProblemAsync(status, HttpStatusCode.BadRequest, InventoryErrorCodes.ValidationFailed);
        await TestClient.AssertProblemAsync(cursor, HttpStatusCode.BadRequest, ErrorCodes.CursorInvalid);
    }

    [Fact]
    public async Task Product_WithoutStock_IsEmpty_Unknown404_RestrictedMayRead()
    {
        await using var app = await InventoryApp.StartAsync(postgres);
        using var kid = await app.AddMemberAsync(HouseholdRole.Restricted, "kid");

        var product = await kid.GetJsonAsync<InventoryProductDto>($"{app.Inventory}/products/{app.Milk.Id}");
        var list = await kid.GetJsonAsync<CursorPage<InventoryProductDto>>(app.Inventory);
        using var unknown = await kid.GetAsync($"{app.Inventory}/products/{Guid.CreateVersion7()}");

        Assert.Equal((app.Milk.Id, 0, ExpiryStatusDto.NoDate), (product.Product.Id, product.Batches.Count, product.WorstStatus));
        Assert.Empty(product.Totals);
        Assert.Empty(list.Items);
        await TestClient.AssertProblemAsync(unknown, HttpStatusCode.NotFound, CatalogErrorCodes.ProductNotFound);
    }

    [Fact]
    public async Task ForeignHousehold_IsNotVisible()
    {
        await using var app = await InventoryApp.StartAsync(postgres);
        var otherId = await Scenario.CreateHouseholdAsync(app.Owner, "Other");
        var otherFridge = await HouseholdDataSeed.StorageLocationAsync(app.Services, otherId);
        var otherProduct = await HouseholdDataSeed.ProductAsync(app.Services, otherId, "Bread");
        await HouseholdDataSeed.BatchAsync(app.Services, otherId, otherProduct.Id, otherFridge.Id);
        using var member = await app.AddMemberAsync(HouseholdRole.Member, "member");

        var list = await app.Owner.GetJsonAsync<CursorPage<InventoryProductDto>>(app.Inventory);
        using var product = await app.Owner.GetAsync($"{app.Inventory}/products/{otherProduct.Id}");
        using var notMember = await member.GetAsync($"/api/v1/households/{otherId}/inventory");

        Assert.Empty(list.Items);
        await TestClient.AssertProblemAsync(product, HttpStatusCode.NotFound, CatalogErrorCodes.ProductNotFound);
        Assert.Equal(HttpStatusCode.NotFound, notMember.StatusCode);
    }
}
