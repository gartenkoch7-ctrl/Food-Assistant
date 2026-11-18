using System.Net;
using FoodAssistant.Application.Activity;
using FoodAssistant.Application.Checkout;
using FoodAssistant.Application.Common;
using FoodAssistant.Application.Households;
using FoodAssistant.Contracts.Checkout;
using FoodAssistant.Contracts.Common;
using FoodAssistant.Contracts.Households;
using FoodAssistant.Domain.Expiry;
using FoodAssistant.Domain.Inventory;
using FoodAssistant.Domain.Quantities;
using FoodAssistant.Infrastructure.Activity;
using FoodAssistant.Infrastructure.Catalog;
using FoodAssistant.Infrastructure.Inventory;
using FoodAssistant.Infrastructure.Persistence;
using FoodAssistant.Infrastructure.Shopping;
using FoodAssistant.Server.Households;
using FoodAssistant.Server.Tests.Auth;
using FoodAssistant.Server.Tests.Inventory;
using FoodAssistant.Server.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static FoodAssistant.Server.Tests.Checkout.CheckoutApi;

namespace FoodAssistant.Server.Tests.Checkout;

/// <summary>The commit under the change-log lock: re-checks, all-or-nothing on conflicts, household isolation.</summary>
public sealed class CheckoutStoreTests(PostgresContainerFixture postgres)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Commit_EntryChangedAfterPlanning_IsStale_OthersAreApplied()
    {
        await using var app = await InventoryApp.StartAsync(postgres);
        var listId = await DefaultListAsync(app);
        var milk = await AddItemAsync(app, listId, app.Milk.Id);
        var bread = await AddItemAsync(app, listId, text: "Bread");
        await SetCheckedAsync(app, await SetCheckedAsync(app, bread, false), true);

        var result = await CommitAsync(app, listId, Line(milk.Id, milk.Version, app.Milk.Id, app.Fridge), new CheckoutCommitLine(bread.Id, bread.Version, null));

        var committed = Assert.IsType<CheckoutCommitted>(result.Value);
        Assert.Equal(milk.Id, Assert.Single(committed.Applied).ItemId);
        Assert.Equal(new[] { bread.Id }, committed.StaleItemIds);
        Assert.Single(await app.RowsAsync<StockBatchRecord>());
        Assert.Equal(bread.Id, Assert.Single(await app.RowsAsync<ShoppingItem>()).Id);
    }

    [Fact]
    public async Task Commit_ProductArchivedAfterPlanning_IsStale_OthersAreApplied()
    {
        await using var app = await InventoryApp.StartAsync(postgres);
        var listId = await DefaultListAsync(app);
        var butter = await HouseholdDataSeed.ProductAsync(app.Services, app.HouseholdId, "Butter");
        var milk = await AddItemAsync(app, listId, app.Milk.Id);
        var butterItem = await AddItemAsync(app, listId, butter.Id);
        await UpdateProductAsync(app, app.Milk.Id, p => p.ArchivedAt = DateTimeOffset.UtcNow);

        var result = await CommitAsync(
            app,
            listId,
            Line(milk.Id, milk.Version, app.Milk.Id, app.Fridge),
            Line(butterItem.Id, butterItem.Version, butter.Id, app.Fridge));

        var committed = Assert.IsType<CheckoutCommitted>(result.Value);
        Assert.Equal(new[] { milk.Id }, committed.StaleItemIds);
        Assert.Equal(butter.Id, Assert.Single(await app.RowsAsync<StockBatchRecord>()).ProductId);
        Assert.Equal(milk.Id, Assert.Single(await app.RowsAsync<ShoppingItem>()).Id);
        Assert.DoesNotContain(await app.RowsAsync<HouseholdActivity>(), a => a.Area == ActivityArea.Inventory && a.ProductId == app.Milk.Id);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Commit_LocationDeletedOrRetyped_409_NothingStored(bool delete)
    {
        await using var app = await InventoryApp.StartAsync(postgres);
        var listId = await DefaultListAsync(app);
        var cellar = await HouseholdDataSeed.StorageLocationAsync(app.Services, app.HouseholdId, "Cellar", StorageType.Ambient);
        var milk = await AddItemAsync(app, listId, app.Milk.Id);
        var cheese = await AddItemAsync(app, listId, text: "Cheese");
        await WithDbAsync(app, async db =>
        {
            var location = await db.Set<StorageLocation>().SingleAsync(l => l.Id == cellar.Id, Ct);
            if (delete)
            {
                db.Remove(location);
            }
            else
            {
                location.Type = StorageType.Frozen;
            }

            await db.SaveChangesAsync(Ct);
        });

        var result = await CommitAsync(
            app,
            listId,
            Line(milk.Id, milk.Version, app.Milk.Id, app.Fridge),
            Line(cheese.Id, cheese.Version, app.Milk.Id, cellar, StorageType.Ambient));

        Assert.Equal((OperationStatus.Conflict, ErrorCodes.Conflict), (result.Status, result.ErrorCode));
        Assert.Empty(await app.RowsAsync<StockBatchRecord>());
        Assert.DoesNotContain(await app.RowsAsync<HouseholdActivity>(), a => a.Area == ActivityArea.Inventory);
        Assert.Equal(2, (await app.RowsAsync<ShoppingItem>()).Count);
    }

    [Fact]
    public async Task Complete_ProductOrLocationOfAnotherHousehold_400NotFound_NothingStored()
    {
        await using var app = await InventoryApp.StartAsync(postgres);
        var otherId = await Scenario.CreateHouseholdAsync(app.Owner, "Other");
        var otherFridge = await HouseholdDataSeed.StorageLocationAsync(app.Services, otherId);
        var otherProduct = await HouseholdDataSeed.ProductAsync(app.Services, otherId, "Bread");
        var listId = await DefaultListAsync(app);
        await AddItemAsync(app, listId, app.Milk.Id);
        var entry = Assert.Single((await ProposeAsync(app, listId)).Entries);

        using var response = await SendCompleteAsync(app, listId, [Book(entry) with { ProductId = otherProduct.Id, StorageLocationId = otherFridge.Id }]);

        await TestClient.AssertProblemAsync(response, HttpStatusCode.BadRequest, CheckoutErrorCodes.ValidationFailed);
        var body = await response.Content.ReadAsStringAsync(Ct);
        Assert.Contains("lines[0].productId", body, StringComparison.Ordinal);
        Assert.Contains("lines[0].storageLocationId", body, StringComparison.Ordinal);
        Assert.Empty(await app.RowsAsync<StockBatchRecord>());
        Assert.Single((await ProposeAsync(app, listId)).Entries);
    }

    private static CheckoutCommitLine Line(Guid itemId, long version, Guid productId, StorageLocation location, StorageType? type = null)
    {
        var batch = new StockBatch(Guid.CreateVersion7(), new Quantity(1m, Unit.Litre), type ?? location.Type, null, ExpiryKind.None);
        var activity = new ActivityEntry(ActivityArea.Inventory, "batch_added", batch.Id, productId, null, null, null);
        return new CheckoutCommitLine(itemId, version, new CheckoutBatch(batch, productId, null, location.Id, null, activity));
    }

    private static async Task<OperationResult<CheckoutCommitted>> CommitAsync(InventoryApp app, Guid listId, params CheckoutCommitLine[] lines)
    {
        await using var scope = Scope(app);
        var commit = new CheckoutCommit(listId, DateTimeOffset.UtcNow, app.Today(), lines);
        return await scope.ServiceProvider.GetRequiredService<ICheckoutStore>().CommitAsync(commit, Ct);
    }

    private static async Task WithDbAsync(InventoryApp app, Func<AppDbContext, Task> action)
    {
        await using var scope = Scope(app);
        await action(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    private static AsyncServiceScope Scope(InventoryApp app)
    {
        var scope = app.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<HttpHouseholdContext>()
            .SetMembership(new HouseholdMembershipInfo(app.HouseholdId, Guid.Empty, HouseholdRole.Owner));
        return scope;
    }
}
