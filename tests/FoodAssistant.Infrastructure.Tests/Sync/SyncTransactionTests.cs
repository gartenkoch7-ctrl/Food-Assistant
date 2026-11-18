using System.Transactions;
using FoodAssistant.Infrastructure.Catalog;
using Microsoft.EntityFrameworkCore;

namespace FoodAssistant.Infrastructure.Tests.Sync;

/// <summary>Announcements follow the transaction outcome, also on unusual paths.</summary>
public sealed class SyncTransactionTests(PostgresFixture postgres)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task TransactionDisposedWithoutCommit_IsNeverAnnounced()
    {
        await using var database = await TestDatabase.CreateAsync(postgres.NewDatabaseConnectionString());
        var (a, _) = await SyncTestData.TwoHouseholdsAsync(database);
        await using var scope = database.CreateScope(activeHousehold: a);

        await using (await scope.Db.Database.BeginTransactionAsync(Ct))
        {
            scope.Db.Add(SyncChangeInterceptorTests.NewProduct(a));
            await scope.Db.SaveChangesAsync(Ct);
        }

        var kept = SyncChangeInterceptorTests.NewProduct(a);
        scope.Db.Add(kept);
        await scope.Db.SaveChangesAsync(Ct);

        Assert.Equal([kept.Id], database.Notifier.AllChanges.Select(c => c.EntityId));
        Assert.Equal([kept.Id], (await SyncTestData.ChangeLogAsync(database, a)).Select(e => e.EntityId));
        Assert.Equal(AutoTransactionBehavior.WhenNeeded, scope.Db.Database.AutoTransactionBehavior);
    }

    [Fact]
    public async Task DisposedTransaction_ThenSynchronousBegin_AnnouncesOnlyTheNewChanges()
    {
        await using var database = await TestDatabase.CreateAsync(postgres.NewDatabaseConnectionString());
        var (a, _) = await SyncTestData.TwoHouseholdsAsync(database);
        await using var scope = database.CreateScope(activeHousehold: a);

        await using (await scope.Db.Database.BeginTransactionAsync(Ct))
        {
            scope.Db.Add(SyncChangeInterceptorTests.NewProduct(a));
            await scope.Db.SaveChangesAsync(Ct);
        }

        var kept = SyncChangeInterceptorTests.NewProduct(a);
        using (var transaction = scope.Db.Database.BeginTransaction())
        {
            scope.Db.Add(kept);
            await scope.Db.SaveChangesAsync(Ct);
            await transaction.CommitAsync(Ct);
        }

        Assert.Equal([kept.Id], database.Notifier.AllChanges.Select(c => c.EntityId));
    }

    [Fact]
    public async Task FailedSaveInsideTransaction_OnlyTheSuccessfulSavesAreAnnounced()
    {
        await using var database = await TestDatabase.CreateAsync(postgres.NewDatabaseConnectionString());
        var (a, _) = await SyncTestData.TwoHouseholdsAsync(database);
        await using var scope = database.CreateScope(activeHousehold: a);

        await using var transaction = await scope.Db.Database.BeginTransactionAsync(Ct);
        var first = SyncChangeInterceptorTests.NewProduct(a);
        scope.Db.Add(first);
        await scope.Db.SaveChangesAsync(Ct);
        var second = SyncChangeInterceptorTests.NewProduct(a);
        second.Name = new string('x', 121); // longer than the column allows
        scope.Db.Add(second);
        await Assert.ThrowsAsync<DbUpdateException>(() => scope.Db.SaveChangesAsync(Ct));
        second.Name = "Bread";
        await scope.Db.SaveChangesAsync(Ct);
        await transaction.CommitAsync(Ct);

        Assert.Equal([first.Id, second.Id], database.Notifier.AllChanges.Select(c => c.EntityId));
        Assert.Equal([first.Id, second.Id], (await SyncTestData.ChangeLogAsync(database, a)).Select(e => e.EntityId));
    }

    [Fact]
    public async Task ConcurrencyConflict_IsNotAnnounced()
    {
        await using var database = await TestDatabase.CreateAsync(postgres.NewDatabaseConnectionString());
        var (a, _) = await SyncTestData.TwoHouseholdsAsync(database);
        Guid productId;
        await using (var seed = database.CreateScope(activeHousehold: a))
        {
            productId = (await M1Seed.ProductAsync(seed.Db, a)).Id;
        }

        await using var stale = database.CreateScope(activeHousehold: a);
        await using var winner = database.CreateScope(activeHousehold: a);
        var staleProduct = await stale.Db.Set<Product>().SingleAsync(p => p.Id == productId, Ct);
        (await winner.Db.Set<Product>().SingleAsync(p => p.Id == productId, Ct)).Name = "Winner";
        await winner.Db.SaveChangesAsync(Ct);
        database.Notifier.Reset();

        staleProduct.Name = "Loser";
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => stale.Db.SaveChangesAsync(Ct));

        Assert.Empty(database.Notifier.Published);
        Assert.Equal(2, (await SyncTestData.ChangeLogAsync(database, a)).Count);
    }

    [Fact]
    public async Task SynchronousCommit_IsAnnouncedInTheBackground()
    {
        await using var database = await TestDatabase.CreateAsync(postgres.NewDatabaseConnectionString());
        var (a, _) = await SyncTestData.TwoHouseholdsAsync(database);
        await using var scope = database.CreateScope(activeHousehold: a);

        await using var transaction = await scope.Db.Database.BeginTransactionAsync(Ct);
        scope.Db.Add(SyncChangeInterceptorTests.NewProduct(a));
        await scope.Db.SaveChangesAsync(Ct);
        transaction.Commit();

        for (var i = 0; i < 50 && database.Notifier.AllChanges.Count == 0; i++)
        {
            await Task.Delay(100, Ct);
        }

        Assert.Single(database.Notifier.AllChanges);
    }

    [Fact]
    public async Task AmbientTransactionScope_IsRejected()
    {
        await using var database = await TestDatabase.CreateAsync(postgres.NewDatabaseConnectionString());
        var (a, _) = await SyncTestData.TwoHouseholdsAsync(database);
        await using var scope = database.CreateScope(activeHousehold: a);

        using var ambient = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);
        scope.Db.Add(SyncChangeInterceptorTests.NewProduct(a));

        await Assert.ThrowsAsync<NotSupportedException>(() => scope.Db.SaveChangesAsync(Ct));
    }
}
