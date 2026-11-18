using FoodAssistant.Application.Households;
using FoodAssistant.Contracts.Households;
using FoodAssistant.Infrastructure.Inventory;
using FoodAssistant.Infrastructure.Persistence;
using FoodAssistant.Infrastructure.Shopping;
using FoodAssistant.Server.Households;
using FoodAssistant.Server.Tests.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FoodAssistant.Server.Tests.Support;

/// <summary>The seed helper writes into the requested household of the running test server.</summary>
public sealed class HouseholdDataSeedTests(PostgresContainerFixture postgres)
{
    [Fact]
    public async Task Seed_WritesConsistentDataIntoTheHousehold()
    {
        await using var app = new ServerTestApp(postgres.NewDatabaseConnectionString());
        var (admin, setup) = await Scenario.SetupAsync(app);
        using var _ = admin;
        var householdId = setup.HouseholdId;

        var location = await HouseholdDataSeed.StorageLocationAsync(app.Services, householdId);
        var product = await HouseholdDataSeed.ProductAsync(app.Services, householdId, "Milk", barcodes: "4000000000001");
        await HouseholdDataSeed.BatchAsync(app.Services, householdId, product.Id, location.Id, new DateOnly(2026, 11, 20));
        await HouseholdDataSeed.StoreAsync(app.Services, householdId);
        var list = await HouseholdDataSeed.ShoppingListAsync(app.Services, householdId);
        await HouseholdDataSeed.ShoppingItemAsync(app.Services, householdId, list.Id, productId: product.Id);

        await using var scope = app.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<HttpHouseholdContext>()
            .SetMembership(new HouseholdMembershipInfo(householdId, Guid.Empty, HouseholdRole.Owner));
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var ct = TestContext.Current.CancellationToken;

        var batch = await db.Set<StockBatchRecord>().SingleAsync(ct);
        var item = await db.Set<ShoppingItem>().SingleAsync(ct);
        Assert.Equal((product.Id, location.Id), (batch.ProductId, batch.StorageLocationId));
        Assert.Equal(product.Id, item.ProductId);
    }
}
