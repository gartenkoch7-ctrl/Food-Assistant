using FoodAssistant.Application.Activity;
using FoodAssistant.Domain.Catalog;
using FoodAssistant.Domain.Expiry;
using FoodAssistant.Domain.Inventory;
using FoodAssistant.Domain.Shopping;
using FoodAssistant.Infrastructure.Activity;
using FoodAssistant.Infrastructure.Catalog;
using FoodAssistant.Infrastructure.Inventory;
using FoodAssistant.Infrastructure.Persistence;
using FoodAssistant.Infrastructure.ShelfLife;
using FoodAssistant.Infrastructure.Shopping;
using FoodAssistant.Infrastructure.Stores;
using FoodAssistant.Infrastructure.Sync;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace FoodAssistant.Infrastructure.Tests;

/// <summary>Schema of milestone M1: household separation, constraints and filters (migration M1Core).</summary>
public sealed class M1SchemaTests(PostgresFixture postgres)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task EveryM1Entity_OnlyActiveHouseholdIsVisible()
    {
        await using var database = await TestDatabase.CreateAsync(postgres.NewDatabaseConnectionString());
        var (a, b) = await SeedTwoHouseholdsAsync(database);

        foreach (var household in new[] { a, b })
        {
            await using var scope = database.CreateScope(activeHousehold: household);
            await AssertOnlyAsync<StorageLocation>(scope.Db, household);
            await AssertOnlyAsync<Product>(scope.Db, household);
            await AssertOnlyAsync<ProductBarcode>(scope.Db, household);
            await AssertOnlyAsync<StockBatchRecord>(scope.Db, household);
            await AssertOnlyAsync<ConsumptionEvent>(scope.Db, household);
            await AssertOnlyAsync<ShelfLifeOverride>(scope.Db, household);
            await AssertOnlyAsync<ShoppingList>(scope.Db, household);
            await AssertOnlyAsync<ShoppingItem>(scope.Db, household);
            await AssertOnlyAsync<RecurringShoppingRule>(scope.Db, household);
            await AssertOnlyAsync<Store>(scope.Db, household);
            await AssertOnlyAsync<StoreSectionOrder>(scope.Db, household);
            await AssertOnlyAsync<StoreSectionTransition>(scope.Db, household);
            await AssertOnlyAsync<ChangeLogEntry>(scope.Db, household);
            await AssertOnlyAsync<HouseholdActivity>(scope.Db, household);
        }

        await using var none = database.CreateScope();
        Assert.Empty(await none.Db.Set<Product>().ToListAsync(Ct));
        Assert.Empty(await none.Db.Set<ShoppingItem>().ToListAsync(Ct));
    }

    [Fact]
    public async Task StockBatch_RoundTripsThroughTheDatabase()
    {
        await using var database = await TestDatabase.CreateAsync(postgres.NewDatabaseConnectionString());
        var (a, _) = await SeedTwoHouseholdsAsync(database);
        await using var scope = database.CreateScope(activeHousehold: a);

        var record = await scope.Db.Set<StockBatchRecord>().AsNoTracking().SingleAsync(Ct);
        var batch = record.ToDomain();

        Assert.Equal(BatchState.Sealed, batch.State);
        Assert.Equal(new DateOnly(2026, 11, 20), batch.ExpiresOn);
        Assert.NotEqual(0u, record.Version);
    }

    [Fact]
    public async Task ShoppingList_SecondDefaultListInHousehold_IsRejected()
    {
        await using var database = await TestDatabase.CreateAsync(postgres.NewDatabaseConnectionString());
        var (a, _) = await SeedTwoHouseholdsAsync(database);
        await using var scope = database.CreateScope(activeHousehold: a);

        await M1Seed.ShoppingListAsync(scope.Db, a, isDefault: false, name: "Hardware store");
        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => M1Seed.ShoppingListAsync(scope.Db, a, isDefault: true));

        Assert.Equal(PostgresErrorCodes.UniqueViolation, Assert.IsType<PostgresException>(exception.InnerException).SqlState);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public async Task ShelfLifeOverride_NeedsExactlyCategoryOrProduct(bool withCategory, bool withProduct)
    {
        await using var database = await TestDatabase.CreateAsync(postgres.NewDatabaseConnectionString());
        var (a, _) = await SeedTwoHouseholdsAsync(database);
        await using var scope = database.CreateScope(activeHousehold: a);
        var product = await scope.Db.Set<Product>().SingleAsync(Ct);

        scope.Db.Add(Override(a, withCategory ? ProductCategories.Cheese : null, withProduct ? product.Id : null));
        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => scope.Db.SaveChangesAsync(Ct));

        Assert.Equal(PostgresErrorCodes.CheckViolation, Assert.IsType<PostgresException>(exception.InnerException).SqlState);
    }

    [Fact]
    public async Task ShoppingItems_SoftDeleted_AreHiddenByTheNotDeletedFilter()
    {
        await using var database = await TestDatabase.CreateAsync(postgres.NewDatabaseConnectionString());
        var (a, _) = await SeedTwoHouseholdsAsync(database);
        await using var scope = database.CreateScope(activeHousehold: a);
        var list = await scope.Db.Set<ShoppingList>().SingleAsync(Ct);
        var deleted = await M1Seed.ShoppingItemAsync(scope.Db, a, list.Id, "Gone");
        deleted.DeletedAt = DateTimeOffset.UtcNow;
        await scope.Db.SaveChangesAsync(Ct);

        var visible = await scope.Db.Set<ShoppingItem>().AsNoTracking().Select(i => i.Text).ToListAsync(Ct);
        var withTombstones = await scope.Db.Set<ShoppingItem>()
            .IgnoreQueryFilters([AppDbContext.NotDeletedFilter])
            .Select(i => i.HouseholdId)
            .ToListAsync(Ct);

        Assert.DoesNotContain("Gone", visible);
        Assert.Single(visible);
        Assert.Equal([a, a], withTombstones);
    }

    [Fact]
    public async Task Household_ExpiryLeadDays_DefaultsToTwoAndKeepsZero()
    {
        await using var database = await TestDatabase.CreateAsync(postgres.NewDatabaseConnectionString());
        var (a, b) = await SeedTwoHouseholdsAsync(database);
        await using (var scope = database.CreateScope())
        {
            var household = await scope.Db.Households.SingleAsync(h => h.Id == b, Ct);
            household.ExpiryLeadDays = 0;
            await scope.Db.SaveChangesAsync(Ct);
        }

        await using var check = database.CreateScope();
        var leadDays = await check.Db.Households.ToDictionaryAsync(h => h.Id, h => h.ExpiryLeadDays, Ct);

        Assert.Equal(2, leadDays[a]);
        Assert.Equal(0, leadDays[b]);
    }

    [Fact]
    public async Task Household_FinalRemoval_CascadesThroughAllM1Data()
    {
        await using var database = await TestDatabase.CreateAsync(postgres.NewDatabaseConnectionString());
        var (a, b) = await SeedTwoHouseholdsAsync(database);
        await using var scope = database.CreateScope();

        var removed = await scope.Db.Households.Where(h => h.Id == a).ExecuteDeleteAsync(Ct);

        scope.Household.Activate(b);
        Assert.Equal(1, removed);
        Assert.Single(await scope.Db.Set<StockBatchRecord>().ToListAsync(Ct));
        scope.Household.Activate(a);
        Assert.Empty(await scope.Db.Set<StockBatchRecord>().ToListAsync(Ct));
        Assert.Empty(await scope.Db.Set<Product>().ToListAsync(Ct));
    }

    private static async Task AssertOnlyAsync<T>(AppDbContext db, Guid householdId)
        where T : class, IHouseholdOwned
    {
        var owners = await db.Set<T>().Select(e => e.HouseholdId).ToListAsync(Ct);
        Assert.NotEmpty(owners);
        Assert.All(owners, owner => Assert.Equal(householdId, owner));
    }

    private static ShelfLifeOverride Override(Guid householdId, string? categoryKey, Guid? productId) => new()
    {
        HouseholdId = householdId,
        CategoryKey = categoryKey,
        ProductId = productId,
        StorageType = StorageType.Chilled,
        DaysAfterOpening = 3,
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow,
    };

    private static async Task<(Guid A, Guid B)> SeedTwoHouseholdsAsync(TestDatabase database)
    {
        await using var scope = database.CreateScope();
        var user = await Seed.UserAsync(scope.Db, "owner");
        var a = await Seed.HouseholdAsync(scope.Db, user.Id, "A");
        var b = await Seed.HouseholdAsync(scope.Db, user.Id, "B");

        // Writes are not filtered: the household id is set explicitly.
        await SeedHouseholdDataAsync(scope.Db, a.Id, "4000000000001");
        await SeedHouseholdDataAsync(scope.Db, b.Id, "4000000000002");
        return (a.Id, b.Id);
    }

    private static async Task SeedHouseholdDataAsync(AppDbContext db, Guid householdId, string barcode)
    {
        var now = DateTimeOffset.UtcNow;
        var location = await M1Seed.StorageLocationAsync(db, householdId);
        var product = await M1Seed.ProductAsync(db, householdId, "Milk", ProductCategories.Dairy, barcode);
        var batch = await M1Seed.BatchAsync(db, householdId, product.Id, location.Id, new DateOnly(2026, 11, 20));
        var store = await M1Seed.StoreAsync(db, householdId);
        var list = await M1Seed.ShoppingListAsync(db, householdId);
        await M1Seed.ShoppingItemAsync(db, householdId, list.Id, productId: product.Id);

        db.Add(Override(householdId, ProductCategories.Dairy, null));
        db.Add(new ConsumptionEvent
        {
            HouseholdId = householdId,
            ProductId = product.Id,
            BatchId = batch.Id,
            Kind = ConsumptionKind.Partial,
            QuantityValue = 0.25m,
            QuantityUnitCode = "l",
            ExpiryStatusAtEvent = ExpiryStatus.Ok,
            OccurredAt = now,
            OccurredOn = new DateOnly(2026, 11, 18),
            CreatedAt = now,
        });
        db.Add(new RecurringShoppingRule
        {
            HouseholdId = householdId,
            ListId = list.Id,
            Text = "Milk",
            Frequency = RecurrenceFrequency.Weekly,
            DayOfWeek = (int)DayOfWeek.Monday,
            NextDueOn = new DateOnly(2026, 11, 23),
            CreatedAt = now,
            UpdatedAt = now,
        });
        db.Add(new StoreSectionOrder { HouseholdId = householdId, StoreId = store.Id, SectionKey = StoreSections.Bakery, Position = 0 });
        db.Add(new StoreSectionTransition
        {
            HouseholdId = householdId,
            StoreId = store.Id,
            FromSectionKey = StoreSections.Produce,
            ToSectionKey = StoreSections.Bakery,
            Count = 1,
        });
        db.Add(new ChangeLogEntry { HouseholdId = householdId, EntityType = "product", EntityId = product.Id, ChangedAt = now });
        db.Add(new HouseholdActivity
        {
            HouseholdId = householdId,
            Area = ActivityArea.Inventory,
            Kind = "batch_added",
            SubjectId = batch.Id,
            ProductId = product.Id,
            OccurredAt = now,
            PayloadJson = "{\"quantity\":1}",
            CreatedAt = now,
            UpdatedAt = now,
        });
        await db.SaveChangesAsync(Ct);
    }
}
