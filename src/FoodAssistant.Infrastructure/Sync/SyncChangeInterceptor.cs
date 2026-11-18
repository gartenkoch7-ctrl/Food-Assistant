using System.Transactions;
using FoodAssistant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;

namespace FoodAssistant.Infrastructure.Sync;

/// <summary>
/// The central sync seam (ADR-0005): every save of an <see cref="ISyncTracked"/> entity also writes
/// a <see cref="ChangeLogEntry"/> in the same transaction and announces it after the commit.
/// </summary>
/// <remarks>
/// <para>
/// Ordering: clients read "changes after sequence N". Identity values are handed out when a row is
/// inserted, not when it commits, so two writers could commit in the opposite order and a reader
/// would skip the earlier sequence for good. Therefore every change-log write takes a per-household
/// advisory lock until the end of its transaction (<see cref="PostgresLocks.LockHouseholdChangeLogAsync"/>):
/// within a household, sequences become visible strictly in ascending order. Writers of different
/// households do not wait for each other. With an explicit transaction the lock is taken here; for
/// the transaction EF Core starts itself, <see cref="SyncTransactionInterceptor"/> takes it.
/// </para>
/// <para>
/// Announcing: without an explicit transaction right after the save, otherwise after the commit
/// (<see cref="SyncTransactionInterceptor"/>); nothing after a rollback.
/// </para>
/// </remarks>
internal sealed class SyncChangeInterceptor(
    PendingChangeNotifications pending,
    SyncChangePublisher publisher,
    TimeProvider time) : SaveChangesInterceptor
{
    private IReadOnlyList<ChangeLogEntry> _currentSave = [];
    private bool _restoreWhenNeeded;

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        ArgumentNullException.ThrowIfNull(eventData);
        if (eventData.Context is { } context && ChangedEntries(context).Count > 0)
        {
            throw new NotSupportedException(
                "Synchronised entities must be saved with SaveChangesAsync (the change-log lock and live updates are asynchronous).");
        }

        return result;
    }

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventData);
        if (eventData.Context is { } context)
        {
            if (context.Database.CurrentTransaction is null)
            {
                // Still pending outside a transaction means: a transaction was disposed without commit
                // or rollback (no interceptor event fires then). Its changes never committed.
                pending.Clear();
            }

            await RecordAsync(context, cancellationToken).ConfigureAwait(false);
        }

        return result;
    }

    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventData);
        EndSave(eventData.Context);
        if (eventData.Context?.Database.CurrentTransaction is null)
        {
            await publisher.PublishPendingAsync(cancellationToken).ConfigureAwait(false);
        }

        return result;
    }

    public override void SaveChangesFailed(DbContextErrorEventData eventData) => Discard(eventData?.Context);

    public override Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        Discard(eventData?.Context);
        return Task.CompletedTask;
    }

    public override void SaveChangesCanceled(DbContextEventData eventData) => Discard(eventData?.Context);

    public override Task SaveChangesCanceledAsync(DbContextEventData eventData, CancellationToken cancellationToken = default)
    {
        Discard(eventData?.Context);
        return Task.CompletedTask;
    }

    // Copied into a list first: adding change-log entries while enumerating the tracker would break the enumeration.
    private static List<EntityEntry<ISyncTracked>> ChangedEntries(DbContext context) =>
        [.. context.ChangeTracker.Entries<ISyncTracked>()
            .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)];

    private async Task RecordAsync(DbContext context, CancellationToken cancellationToken)
    {
        var changed = ChangedEntries(context);
        if (changed.Count == 0)
        {
            return;
        }

        var now = time.GetUtcNow();
        var entries = changed.Select(e => new ChangeLogEntry
        {
            HouseholdId = e.Entity.HouseholdId,
            EntityType = SyncEntityTypeMap.KeyOf(e.Metadata.ClrType),
            EntityId = e.Entity.Id,
            Deleted = e.State == EntityState.Deleted || e.Entity is ISoftDeletable { DeletedAt: not null },
            ChangedAt = now,
        }).ToList();

        if (context.Database.CurrentTransaction is { } transaction)
        {
            foreach (var household in entries.Select(e => e.HouseholdId).Distinct().Order())
            {
                await PostgresLocks.LockHouseholdChangeLogAsync(transaction.GetDbTransaction(), household, cancellationToken)
                    .ConfigureAwait(false);
            }
        }
        else
        {
            RequireOwnTransaction(context.Database);
        }

        foreach (var entry in entries)
        {
            context.Add(entry);
            pending.Add(entry);
        }

        _currentSave = entries;
        pending.SaveInProgress = true;
    }

    // A failed save must not leave its entries behind: a retry would write them twice, and the
    // failed changes must never be announced. Entries of earlier saves in the transaction stay.
    private void Discard(DbContext? context)
    {
        pending.Remove(_currentSave);
        if (context is not null)
        {
            foreach (var entry in _currentSave)
            {
                context.Entry(entry).State = EntityState.Detached;
            }
        }

        EndSave(context);
    }

    // The lock lives in the transaction EF Core starts for this save (SyncTransactionInterceptor).
    // Without one (ambient TransactionScope, Never) the ordering guarantee would silently break.
    private void RequireOwnTransaction(DatabaseFacade database)
    {
        if (Transaction.Current is not null
            || database.GetEnlistedTransaction() is not null
            || database.AutoTransactionBehavior == AutoTransactionBehavior.Never)
        {
            throw new NotSupportedException(
                "Synchronised entities need an EF Core transaction: no ambient TransactionScope and no AutoTransactionBehavior.Never.");
        }

        if (database.AutoTransactionBehavior == AutoTransactionBehavior.WhenNeeded)
        {
            // EF Core skips its own transaction for a single command batch; the lock needs one.
            database.AutoTransactionBehavior = AutoTransactionBehavior.Always;
            _restoreWhenNeeded = true;
        }
    }

    private void EndSave(DbContext? context)
    {
        _currentSave = [];
        pending.SaveInProgress = false;
        if (_restoreWhenNeeded && context is not null)
        {
            context.Database.AutoTransactionBehavior = AutoTransactionBehavior.WhenNeeded;
        }

        _restoreWhenNeeded = false;
    }
}
