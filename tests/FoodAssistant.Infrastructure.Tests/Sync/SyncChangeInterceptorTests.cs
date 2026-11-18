using FoodAssistant.Contracts.Sync;
using FoodAssistant.Infrastructure.Catalog;
using Microsoft.EntityFrameworkCore;

namespace FoodAssistant.Infrastructure.Tests.Sync;

/// <summary>The sync interceptor writes the change log and announces it after the commit (ADR-0005).</summary>
public sealed class SyncChangeInterceptorTests(PostgresFixture postgres)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task AddedModifiedDeletedAndSoftDeleted_WriteOneRowEach_AndAreAnnounced()
    {
        await using var database = await TestDatabase.CreateAsync(postgres.NewDatabaseConnectionString());
        var (a, _) = await SyncTestData.TwoHouseholdsAsync(database);
        await using var scope = database.CreateScope(activeHousehold: a);

        var product = await M1Seed.ProductAsync(scope.Db, a);
        product.Name = "Oat milk";
        await scope.Db.SaveChangesAsync(Ct);
        var location = await M1Seed.StorageLocationAsync(scope.Db, a);
        scope.Db.Remove(location);
        await scope.Db.SaveChangesAsync(Ct);
        var list = await M1Seed.ShoppingListAsync(scope.Db, a);
        var item = await M1Seed.ShoppingItemAsync(scope.Db, a, list.Id);
        item.DeletedAt = DateTimeOffset.UtcNow;
        item.Version++;
        await scope.Db.SaveChangesAsync(Ct);

        var log = await SyncTestData.ChangeLogAsync(database, a);
        Assert.Equal(
            [
                (SyncEntityTypes.Product, product.Id, false),
                (SyncEntityTypes.Product, product.Id, false),
                (SyncEntityTypes.StorageLocation, location.Id, false),
                (SyncEntityTypes.StorageLocation, location.Id, true),
                (SyncEntityTypes.ShoppingList, list.Id, false),
                (SyncEntityTypes.ShoppingItem, item.Id, false),
                (SyncEntityTypes.ShoppingItem, item.Id, true),
            ],
            log.Select(e => (e.EntityType, e.EntityId, e.Deleted)));

        var announced = database.Notifier.AllChanges;
        Assert.Equal(log.Select(e => e.Sequence), announced.Select(c => c.Sequence));
        Assert.All(announced, c => Assert.True(c.Sequence > 0));
        Assert.All(database.Notifier.Published, p => Assert.Equal(a, p.HouseholdId));
    }

    [Fact]
    public async Task OneSaveInTwoHouseholds_IsAnnouncedPerHousehold()
    {
        await using var database = await TestDatabase.CreateAsync(postgres.NewDatabaseConnectionString());
        var (a, b) = await SyncTestData.TwoHouseholdsAsync(database);
        await using var scope = database.CreateScope();

        scope.Db.Add(NewProduct(a));
        scope.Db.Add(NewProduct(b));
        await scope.Db.SaveChangesAsync(Ct);

        var published = database.Notifier.Published.OrderBy(p => p.HouseholdId).ToList();
        Assert.Equal(new[] { a, b }.Order(), published.Select(p => p.HouseholdId));
        Assert.All(published, p => Assert.Single(p.Changes));
    }

    [Fact]
    public async Task ExplicitTransaction_IsAnnouncedOnlyAfterCommit()
    {
        await using var database = await TestDatabase.CreateAsync(postgres.NewDatabaseConnectionString());
        var (a, _) = await SyncTestData.TwoHouseholdsAsync(database);
        await using var scope = database.CreateScope(activeHousehold: a);

        await using var transaction = await scope.Db.Database.BeginTransactionAsync(Ct);
        var product = NewProduct(a);
        scope.Db.Add(product);
        await scope.Db.SaveChangesAsync(Ct);
        product.Name = "Changed";
        await scope.Db.SaveChangesAsync(Ct);
        Assert.Empty(database.Notifier.Published);

        await transaction.CommitAsync(Ct);

        var change = Assert.Single(database.Notifier.AllChanges);
        Assert.Equal(product.Id, change.EntityId);
        Assert.Equal((await SyncTestData.ChangeLogAsync(database, a)).Max(e => e.Sequence), change.Sequence);
    }

    [Fact]
    public async Task ExplicitTransaction_RolledBack_IsNeverAnnounced_AndLeavesNoRows()
    {
        await using var database = await TestDatabase.CreateAsync(postgres.NewDatabaseConnectionString());
        var (a, _) = await SyncTestData.TwoHouseholdsAsync(database);
        await using var scope = database.CreateScope(activeHousehold: a);

        await using (var transaction = await scope.Db.Database.BeginTransactionAsync(Ct))
        {
            scope.Db.Add(NewProduct(a));
            await scope.Db.SaveChangesAsync(Ct);
            await transaction.RollbackAsync(Ct);
        }

        Assert.Empty(database.Notifier.Published);
        Assert.Empty(await SyncTestData.ChangeLogAsync(database, a));
    }

    [Fact]
    public async Task FailedSave_IsNotAnnounced_AndARetryWritesOneRow()
    {
        await using var database = await TestDatabase.CreateAsync(postgres.NewDatabaseConnectionString());
        var (a, _) = await SyncTestData.TwoHouseholdsAsync(database);
        await using var scope = database.CreateScope(activeHousehold: a);
        var product = NewProduct(a);
        product.Name = new string('x', 121); // longer than the column allows

        scope.Db.Add(product);
        await Assert.ThrowsAsync<DbUpdateException>(() => scope.Db.SaveChangesAsync(Ct));
        Assert.Empty(database.Notifier.Published);

        product.Name = "Milk";
        await scope.Db.SaveChangesAsync(Ct);

        Assert.Single(await SyncTestData.ChangeLogAsync(database, a));
        Assert.Single(database.Notifier.AllChanges);
    }

    [Fact]
    public async Task FailingNotifier_DoesNotFailTheSave()
    {
        await using var database = await TestDatabase.CreateAsync(postgres.NewDatabaseConnectionString());
        var (a, _) = await SyncTestData.TwoHouseholdsAsync(database);
        database.Notifier.Fail = true;
        await using var scope = database.CreateScope(activeHousehold: a);

        scope.Db.Add(NewProduct(a));
        await scope.Db.SaveChangesAsync(Ct);

        Assert.Single(await SyncTestData.ChangeLogAsync(database, a));
    }

    [Fact]
    public async Task SynchronousSave_OfSyncTrackedEntity_IsRejected()
    {
        await using var database = await TestDatabase.CreateAsync(postgres.NewDatabaseConnectionString());
        var (a, _) = await SyncTestData.TwoHouseholdsAsync(database);
        await using var scope = database.CreateScope(activeHousehold: a);

        scope.Db.Add(NewProduct(a));

        Assert.Throws<NotSupportedException>(() => scope.Db.SaveChanges());
    }

    internal static Product NewProduct(Guid householdId) => new()
    {
        HouseholdId = householdId,
        Name = "Milk",
        CategoryKey = "dairy",
        DefaultUnitCode = "l",
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow,
    };
}
