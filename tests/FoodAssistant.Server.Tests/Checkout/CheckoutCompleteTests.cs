using System.Net;
using FoodAssistant.Application.Activity;
using FoodAssistant.Contracts.Checkout;
using FoodAssistant.Contracts.Common;
using FoodAssistant.Contracts.Inventory;
using FoodAssistant.Contracts.Shopping;
using FoodAssistant.Contracts.Sync;
using FoodAssistant.Domain.Catalog;
using FoodAssistant.Domain.Expiry;
using FoodAssistant.Domain.Inventory;
using FoodAssistant.Infrastructure.Activity;
using FoodAssistant.Infrastructure.Catalog;
using FoodAssistant.Infrastructure.Inventory;
using FoodAssistant.Infrastructure.Sync;
using FoodAssistant.Server.Tests.Auth;
using FoodAssistant.Server.Tests.Inventory;
using FoodAssistant.Server.Tests.Support;
using static FoodAssistant.Server.Tests.Checkout.CheckoutApi;

namespace FoodAssistant.Server.Tests.Checkout;

/// <summary>Completing a shopping list (F1): batches, removed entries, stale entries, idempotency, validation.</summary>
public sealed class CheckoutCompleteTests(PostgresContainerFixture postgres)
{
    [Fact]
    public async Task Complete_BooksSkipsAndCreates_InOneGo()
    {
        await using var app = await InventoryApp.StartAsync(postgres);
        app.Rules.Set(StorageType.Frozen, new ShelfLifeRule { DaysFrozen = 90 });
        var today = app.Today();
        var listId = await DefaultListAsync(app);
        var peas = await HouseholdDataSeed.ProductAsync(app.Services, app.HouseholdId, "Peas", ProductCategories.Frozen);
        await AddItemAsync(app, listId, app.Milk.Id);
        await AddItemAsync(app, listId, peas.Id);
        await AddItemAsync(app, listId, text: "Bread");
        await AddItemAsync(app, listId, text: "Oat drink");
        var open = await AddItemAsync(app, listId, text: "Later", check: false);
        var entries = (await ProposeAsync(app, listId)).Entries;
        var milkDate = today.AddDays(7);

        var result = await CompleteAsync(
            app,
            listId,
            Book(entries[0]) with { ExpiresOn = milkDate, ExpiryKind = ExpiryKindDto.BestBefore },
            Book(entries[1]),
            Skip(entries[2]),
            Book(entries[3]) with
            {
                NewProduct = new NewProductLine(" Oat drink ", ProductCategories.PlantBased, "l"),
                Quantity = new QuantityDto(1m, "l"),
                StorageLocationId = app.Fridge.Id,
                ExpiresOn = today.AddDays(30),
                ExpiryKind = ExpiryKindDto.BestBefore,
            });

        Assert.Equal((3, 4, 0), (result.BatchesCreated, result.ItemsRemoved, result.StaleItemIds.Count));
        var remaining = await app.Owner.GetJsonAsync<List<ShoppingItemDto>>($"{ShoppingUrl(app)}/lists/{listId}/items?includeChecked=true");
        Assert.Equal(open.Id, Assert.Single(remaining).Id);

        var batches = (await app.RowsAsync<StockBatchRecord>()).ToDictionary(b => b.Id);
        Assert.Equal(result.BatchIds.Order(), batches.Keys.Order());
        Assert.All(batches.Values, b => Assert.Equal((BatchOrigin.Purchase, today), (b.Origin, b.AddedOn)));
        var products = (await app.RowsAsync<Product>()).ToDictionary(p => p.Name);
        var milk = Assert.Single(batches.Values, b => b.ProductId == app.Milk.Id);
        Assert.Equal((app.Fridge.Id, BatchState.Sealed, milkDate, ExpiryKind.BestBefore), (milk.StorageLocationId, milk.State, milk.ExpiresOn, milk.ExpiryKind));
        var frozen = Assert.Single(batches.Values, b => b.ProductId == peas.Id);
        Assert.Equal((BatchState.Frozen, today, today.AddDays(90), ExpiryKind.UseBy), (frozen.State, frozen.FrozenOn, frozen.ExpiresOn, frozen.ExpiryKind));
        var oat = products["Oat drink"];
        Assert.Equal((ProductCategories.PlantBased, "l"), (oat.CategoryKey, oat.DefaultUnitCode));
        Assert.Single(batches.Values, b => b.ProductId == oat.Id);

        // Only user-entered package dates are remembered, rule dates (frozen at home) are not.
        Assert.Equal((7, ExpiryKind.BestBefore), (products["Milk"].LastShelfLifeDays, products["Milk"].LastExpiryKind));
        Assert.Equal((30, ExpiryKind.BestBefore), (oat.LastShelfLifeDays, oat.LastExpiryKind));
        Assert.Null(products["Peas"].LastShelfLifeDays);

        var call = Assert.Single(app.StockHandler.Calls);
        Assert.Equal(new[] { app.Milk.Id, peas.Id, oat.Id }.Order(), call.ProductIds.Order());
        var changes = await app.RowsAsync<ChangeLogEntry>();
        Assert.All(result.BatchIds, id => Assert.Contains(changes, c => c.EntityId == id && c.EntityType == SyncEntityTypes.Batch));
        Assert.Equal(4, changes.Count(c => c.EntityType == SyncEntityTypes.ShoppingItem && c.Deleted));
        var activities = await app.RowsAsync<HouseholdActivity>();
        Assert.Equal(3, activities.Count(a => a.Area == ActivityArea.Inventory && a.Kind == "batch_added"));

        // The proposal now remembers the last purchase.
        await AddItemAsync(app, listId, app.Milk.Id);
        var next = Assert.Single((await ProposeAsync(app, listId)).Entries);
        Assert.Equal((today.AddDays(7), ExpiryKindDto.BestBefore, false), (next.ExpiresOn, next.ExpiryKind, next.ExpiryFromRule));
    }

    [Fact]
    public async Task Complete_ChangedEntry_IsStale_RetryWithSameKey_ReplaysWithoutBookingTwice()
    {
        await using var app = await InventoryApp.StartAsync(postgres);
        var listId = await DefaultListAsync(app);
        await AddItemAsync(app, listId, app.Milk.Id);
        var changed = await AddItemAsync(app, listId, text: "Bread");
        var entries = (await ProposeAsync(app, listId)).Entries;
        await SetCheckedAsync(app, changed, false);
        var key = Guid.CreateVersion7();
        var bread = entries[1] with { ProductId = app.Milk.Id };

        using var first = await SendCompleteAsync(app, listId, [Book(entries[0]), Book(bread)], key);
        var firstBody = await first.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using var retry = await SendCompleteAsync(app, listId, [Book(entries[0]), Book(bread)], key);
        using var newKey = await SendCompleteAsync(app, listId, [Book(entries[0]), Book(bread)], Guid.CreateVersion7());
        var again = await TestClient.ReadAsync<CheckoutResultDto>(newKey);

        var result = await TestClient.ReadAsync<CheckoutResultDto>(first);
        Assert.Equal((1, 1), (result.BatchesCreated, result.ItemsRemoved));
        Assert.Equal(new[] { changed.Id }, result.StaleItemIds);
        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
        Assert.Equal(firstBody, await retry.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal((0, 0), (again.BatchesCreated, again.ItemsRemoved));
        Assert.Equal(new[] { entries[0].ItemId, changed.Id }.Order(), again.StaleItemIds.Order());
        Assert.Single(await app.RowsAsync<StockBatchRecord>());
        Assert.False(Assert.Single(await app.Owner.GetJsonAsync<List<ShoppingItemDto>>($"{ShoppingUrl(app)}/lists/{listId}/items?includeChecked=true")).Checked);
    }

    [Fact]
    public async Task Complete_SelfFrozenWithDate_FrozenByRule_DateNotRemembered()
    {
        await using var app = await InventoryApp.StartAsync(postgres);
        app.Rules.Set(StorageType.Frozen, new ShelfLifeRule { DaysFrozen = 60 });
        var today = app.Today();
        var listId = await DefaultListAsync(app);
        await AddItemAsync(app, listId, app.Milk.Id);
        var entry = Assert.Single((await ProposeAsync(app, listId)).Entries);

        await CompleteAsync(app, listId, Book(entry) with
        {
            StorageLocationId = app.Freezer.Id,
            ExpiresOn = today.AddDays(5),
            ExpiryKind = ExpiryKindDto.UseBy,
            SelfFrozen = true,
        });

        var batch = Assert.Single(await app.RowsAsync<StockBatchRecord>());
        Assert.Equal((BatchState.Frozen, today.AddDays(60), today.AddDays(5)), (batch.State, batch.ExpiresOn, batch.PackageExpiresOn));
        Assert.Null(Assert.Single(await app.RowsAsync<Product>()).LastShelfLifeDays);
    }

    [Fact]
    public async Task Complete_InvalidLines_400WithFields_EmptyRequest_422_NothingStored()
    {
        await using var app = await InventoryApp.StartAsync(postgres);
        var listId = await DefaultListAsync(app);
        await AddItemAsync(app, listId, text: "Bread");
        await AddItemAsync(app, listId, app.Milk.Id);
        await AddItemAsync(app, listId, text: "Tofu");
        var entries = (await ProposeAsync(app, listId)).Entries;
        CheckoutLineRequest[] lines =
        [
            Book(entries[0]),
            Book(entries[1]) with { ExpiresOn = app.Today(), StorageLocationId = Guid.CreateVersion7() },
            Book(entries[2]) with { NewProduct = new NewProductLine("Tofu", "unknown", "l") },
        ];

        using var invalid = await SendCompleteAsync(app, listId, lines);
        using var unknownLocation = await SendCompleteAsync(app, listId, [Book(entries[1]) with { StorageLocationId = Guid.CreateVersion7() }]);
        using var empty = await SendCompleteAsync(app, listId, []);

        await TestClient.AssertProblemAsync(invalid, HttpStatusCode.BadRequest, CheckoutErrorCodes.ValidationFailed);
        var body = await invalid.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("lines[0].productId", body, StringComparison.Ordinal);
        Assert.Contains("lines[1].expiryKind", body, StringComparison.Ordinal);
        Assert.Contains("lines[2].newProduct.categoryKey", body, StringComparison.Ordinal);
        await TestClient.AssertProblemAsync(unknownLocation, HttpStatusCode.BadRequest, CheckoutErrorCodes.ValidationFailed);
        Assert.Contains("lines[0].storageLocationId", await unknownLocation.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), StringComparison.Ordinal);
        await TestClient.AssertProblemAsync(empty, HttpStatusCode.UnprocessableEntity, CheckoutErrorCodes.NothingToComplete);
        Assert.Empty(await app.RowsAsync<StockBatchRecord>());
        Assert.Equal(3, (await ProposeAsync(app, listId)).Entries.Count);
    }

    [Fact]
    public async Task Complete_ArchivedProduct_IsNotFound_FailingHook_KeepsCheckout()
    {
        await using var app = await InventoryApp.StartAsync(postgres);
        app.StockHandler.Fail = true;
        var listId = await DefaultListAsync(app);
        var butter = await HouseholdDataSeed.ProductAsync(app.Services, app.HouseholdId, "Butter");
        await AddItemAsync(app, listId, butter.Id);
        await AddItemAsync(app, listId, app.Milk.Id);
        await UpdateProductAsync(app, butter.Id, p => p.ArchivedAt = DateTimeOffset.UtcNow);
        var entries = (await ProposeAsync(app, listId)).Entries;

        using var archived = await SendCompleteAsync(app, listId, [Book(entries[0]) with { ProductId = butter.Id }]);
        var result = await CompleteAsync(app, listId, Skip(entries[0]), Book(entries[1]));

        Assert.Null(entries[0].ProductId);
        await TestClient.AssertProblemAsync(archived, HttpStatusCode.BadRequest, CheckoutErrorCodes.ValidationFailed);
        Assert.Equal((1, 2), (result.BatchesCreated, result.ItemsRemoved));
        Assert.Single(app.StockHandler.Calls);
    }
}
