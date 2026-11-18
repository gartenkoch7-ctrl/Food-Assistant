using Microsoft.EntityFrameworkCore;

namespace FoodAssistant.Infrastructure.Tests.Sync;

/// <summary>
/// Change-log sequences of one household become visible in ascending order, so "changes after N"
/// never skips a row that commits later with a smaller sequence.
/// </summary>
public sealed class SyncChangeOrderingTests(PostgresFixture postgres)
{
    // Long enough for an unblocked save to finish; a false pass is possible only if the lock were missing AND the save slow.
    private static readonly TimeSpan BlockedFor = TimeSpan.FromMilliseconds(700);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task SecondWriterOfTheSameHousehold_WaitsForTheFirstCommit_AndGetsTheHigherSequence()
    {
        await using var database = await TestDatabase.CreateAsync(postgres.NewDatabaseConnectionString());
        var (a, _) = await SyncTestData.TwoHouseholdsAsync(database);
        await using var first = database.CreateScope(activeHousehold: a);
        await using var second = database.CreateScope(activeHousehold: a);

        await using var transaction = await first.Db.Database.BeginTransactionAsync(Ct);
        first.Db.Add(SyncChangeInterceptorTests.NewProduct(a));
        await first.Db.SaveChangesAsync(Ct);

        // No explicit transaction: the lock is taken in the transaction EF Core starts itself.
        second.Db.Add(SyncChangeInterceptorTests.NewProduct(a));
        var secondSave = second.Db.SaveChangesAsync(Ct);
        await Task.Delay(BlockedFor, Ct);
        Assert.False(secondSave.IsCompleted, "The second writer must wait for the first commit.");

        await transaction.CommitAsync(Ct);
        await secondSave;

        var log = await SyncTestData.ChangeLogAsync(database, a);
        Assert.Equal(2, log.Count);
        Assert.Equal(
            database.Notifier.AllChanges.Select(c => c.Sequence),
            log.Select(e => e.Sequence));
    }

    [Fact]
    public async Task WriterOfAnotherHousehold_DoesNotWait()
    {
        await using var database = await TestDatabase.CreateAsync(postgres.NewDatabaseConnectionString());
        var (a, b) = await SyncTestData.TwoHouseholdsAsync(database);
        await using var first = database.CreateScope(activeHousehold: a);
        await using var other = database.CreateScope(activeHousehold: b);

        await using var transaction = await first.Db.Database.BeginTransactionAsync(Ct);
        first.Db.Add(SyncChangeInterceptorTests.NewProduct(a));
        await first.Db.SaveChangesAsync(Ct);

        other.Db.Add(SyncChangeInterceptorTests.NewProduct(b));
        await other.Db.SaveChangesAsync(Ct).WaitAsync(TimeSpan.FromSeconds(10), Ct);

        await transaction.CommitAsync(Ct);
        Assert.Single(await SyncTestData.ChangeLogAsync(database, b));
    }

    [Fact]
    public async Task ChangeLogSequence_HandsOutValuesWithoutCache()
    {
        // With a sequence cache, sessions draw from their own number blocks and the lock could not order them.
        await using var database = await TestDatabase.CreateAsync(postgres.NewDatabaseConnectionString());
        await using var scope = database.CreateScope();

        var cache = await scope.Db.Database
            .SqlQueryRaw<long>(
                "SELECT seqcache AS \"Value\" FROM pg_sequence WHERE seqrelid = pg_get_serial_sequence('\"ChangeLog\"', 'Sequence')::regclass")
            .SingleAsync(Ct);

        Assert.Equal(1, cache);
    }

    [Fact]
    public async Task ExplicitTransaction_TakesTheLockAtItsFirstSave()
    {
        await using var database = await TestDatabase.CreateAsync(postgres.NewDatabaseConnectionString());
        var (a, _) = await SyncTestData.TwoHouseholdsAsync(database);
        await using var first = database.CreateScope(activeHousehold: a);
        await using var second = database.CreateScope(activeHousehold: a);

        await using var transaction = await first.Db.Database.BeginTransactionAsync(Ct);
        first.Db.Add(SyncChangeInterceptorTests.NewProduct(a));
        await first.Db.SaveChangesAsync(Ct);

        await using var secondTransaction = await second.Db.Database.BeginTransactionAsync(Ct);
        second.Db.Add(SyncChangeInterceptorTests.NewProduct(a));
        var secondSave = second.Db.SaveChangesAsync(Ct);
        await Task.Delay(BlockedFor, Ct);
        Assert.False(secondSave.IsCompleted, "The second transaction must wait for the first commit.");

        await transaction.CommitAsync(Ct);
        await secondSave;
        await secondTransaction.CommitAsync(Ct);

        var log = await SyncTestData.ChangeLogAsync(database, a);
        Assert.Equal(log.Select(e => e.Sequence), database.Notifier.AllChanges.Select(c => c.Sequence));
    }
}
