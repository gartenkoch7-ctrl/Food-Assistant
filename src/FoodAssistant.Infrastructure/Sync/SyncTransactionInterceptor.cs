using System.Data.Common;
using FoodAssistant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace FoodAssistant.Infrastructure.Sync;

/// <summary>
/// Transaction side of <see cref="SyncChangeInterceptor"/>: takes the change-log lock in the
/// transaction EF Core starts for a save, announces pending changes after the commit and drops
/// them after a rollback.
/// </summary>
internal sealed class SyncTransactionInterceptor(
    PendingChangeNotifications pending,
    SyncChangePublisher publisher) : DbTransactionInterceptor
{
    public override async ValueTask<DbTransaction> TransactionStartedAsync(
        DbConnection connection,
        TransactionEndEventData eventData,
        DbTransaction result,
        CancellationToken cancellationToken = default)
    {
        // Inside a save, the entries were recorded just before, without a transaction to lock in.
        // An explicit transaction starts before any save and locks in SyncChangeInterceptor;
        // anything pending then is left from a transaction disposed without commit or rollback.
        if (!pending.SaveInProgress)
        {
            pending.Clear();
            return result;
        }

        foreach (var household in pending.Households)
        {
            await PostgresLocks.LockHouseholdChangeLogAsync(result, household, cancellationToken).ConfigureAwait(false);
        }

        return result;
    }

    // A synchronous begin is always explicit (sync SaveChanges rejects sync-tracked entities), so
    // anything pending is left from a transaction disposed without commit or rollback.
    public override DbTransaction TransactionStarted(DbConnection connection, TransactionEndEventData eventData, DbTransaction result)
    {
        if (!pending.SaveInProgress)
        {
            pending.Clear();
        }

        return result;
    }

    // Inside a save (EF Core's own transaction) the sequences are not yet in the entities;
    // SyncChangeInterceptor.SavedChangesAsync announces them right after.
    public override Task TransactionCommittedAsync(
        DbTransaction transaction,
        TransactionEndEventData eventData,
        CancellationToken cancellationToken = default) =>
        pending.SaveInProgress ? Task.CompletedTask : publisher.PublishPendingAsync(cancellationToken);

    public override void TransactionCommitted(DbTransaction transaction, TransactionEndEventData eventData)
    {
        if (!pending.IsEmpty && !pending.SaveInProgress)
        {
            // Synchronous commit after asynchronous saves: announce in the background. The publisher
            // takes the entries before its first await and never throws.
            _ = publisher.PublishPendingAsync(CancellationToken.None);
        }
    }

    public override Task TransactionRolledBackAsync(
        DbTransaction transaction,
        TransactionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        pending.Clear();
        return Task.CompletedTask;
    }

    public override void TransactionRolledBack(DbTransaction transaction, TransactionEndEventData eventData) =>
        pending.Clear();

    public override Task TransactionFailedAsync(
        DbTransaction transaction,
        TransactionErrorEventData eventData,
        CancellationToken cancellationToken = default)
    {
        pending.Clear();
        return Task.CompletedTask;
    }

    public override void TransactionFailed(DbTransaction transaction, TransactionErrorEventData eventData) =>
        pending.Clear();
}
