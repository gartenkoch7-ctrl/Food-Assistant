using FoodAssistant.Application.Households;
using FoodAssistant.Application.Stores;
using FoodAssistant.Domain.Shopping;
using FoodAssistant.Infrastructure.Persistence;
using FoodAssistant.Infrastructure.Shopping;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace FoodAssistant.Infrastructure.Stores;

/// <summary>Learned transitions, manual order and last check-off of a store; the active-household filter applies.</summary>
internal sealed class StoreSectionOrderStore(AppDbContext db, IHouseholdContext householdContext) : IStoreSectionOrderStore
{
    private DbSet<StoreSectionTransition> Transitions => db.Set<StoreSectionTransition>();

    private DbSet<StoreSectionOrder> ManualOrder => db.Set<StoreSectionOrder>();

    private Guid ActiveHouseholdId =>
        householdContext.HouseholdId ?? throw new InvalidOperationException("Stores need an active household.");

    public async Task<IReadOnlyList<SectionTransition>> GetTransitionsAsync(Guid storeId, CancellationToken cancellationToken) =>
        await Transitions
            .AsNoTracking()
            .Where(t => t.StoreId == storeId)
            .Select(t => new SectionTransition(t.FromSectionKey, t.ToSectionKey, t.Count))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<IReadOnlyDictionary<string, int>> GetManualOrderAsync(Guid storeId, CancellationToken cancellationToken) =>
        await ManualOrder
            .AsNoTracking()
            .Where(o => o.StoreId == storeId)
            .ToDictionaryAsync(o => o.SectionKey, o => o.Position, StringComparer.Ordinal, cancellationToken)
            .ConfigureAwait(false);

    public async Task<bool> SetManualOrderAsync(Guid storeId, IReadOnlyList<string> sections, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sections);
        var householdId = ActiveHouseholdId;
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        // Two people saving at once would otherwise both insert the same keys.
        await db.Database.ExecuteSqlAsync(
                $"SELECT 1 FROM \"Stores\" WHERE \"Id\" = {storeId} AND \"HouseholdId\" = {householdId} FOR UPDATE",
                cancellationToken)
            .ConfigureAwait(false);

        // Not synchronised (no change log), so a bulk delete is allowed here.
        await ManualOrder.Where(o => o.StoreId == storeId).ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        ForgetTracked(storeId);
        ManualOrder.AddRange(sections.Select((key, position) => new StoreSectionOrder
        {
            HouseholdId = householdId,
            StoreId = storeId,
            SectionKey = key,
            Position = position,
        }));
        try
        {
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException exception) when (ShoppingDbErrors.IsForeignKeyViolation(exception))
        {
            // The store was deleted after the caller checked it.
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            db.ChangeTracker.Clear();
            return false;
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    public async Task ClearManualOrderAsync(Guid storeId, CancellationToken cancellationToken)
    {
        await ManualOrder.Where(o => o.StoreId == storeId).ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        ForgetTracked(storeId);
    }

    public async Task<(string? SectionKey, DateTimeOffset? At)?> GetLastCheckAsync(Guid storeId, CancellationToken cancellationToken)
    {
        var last = await db.Set<Store>()
            .AsNoTracking()
            .Where(s => s.Id == storeId)
            .Select(s => new { s.LastCheckedSectionKey, s.LastCheckedAt })
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        return last is null ? null : (last.LastCheckedSectionKey, last.LastCheckedAt);
    }

    public async Task<bool> RecordTransitionAsync(
        Guid storeId,
        string? from,
        string to,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        // Change-log lock first (the store row is synchronised): taking it only at the save, after
        // the counter row lock below, could deadlock with a concurrent store deletion.
        await PostgresLocks.LockHouseholdChangeLogAsync(transaction.GetDbTransaction(), ActiveHouseholdId, cancellationToken)
            .ConfigureAwait(false);
        var store = await db.Set<Store>().SingleOrDefaultAsync(s => s.Id == storeId, cancellationToken).ConfigureAwait(false);
        if (store is null)
        {
            return true;
        }

        if (from is not null)
        {
            // Counters are not synchronised, so the atomic in-database increment is allowed.
            var counted = await Transitions
                .Where(t => t.StoreId == storeId && t.FromSectionKey == from && t.ToSectionKey == to)
                .ExecuteUpdateAsync(t => t.SetProperty(x => x.Count, x => x.Count + 1), cancellationToken)
                .ConfigureAwait(false);
            if (counted == 0)
            {
                Transitions.Add(new StoreSectionTransition
                {
                    HouseholdId = store.HouseholdId,
                    StoreId = storeId,
                    FromSectionKey = from,
                    ToSectionKey = to,
                    Count = 1,
                });
            }
        }

        // The store row (xmin) guards the whole step: a concurrent check-off in the same store
        // makes this save fail, and the caller decides again from the newer last check-off.
        store.LastCheckedSectionKey = to;
        store.LastCheckedAt = at;
        try
        {
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException exception) when (exception is DbUpdateConcurrencyException || ShoppingDbErrors.IsKeyConflict(exception))
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            db.ChangeTracker.Clear();
            return false;
        }

        return true;
    }

    // ExecuteDelete bypasses the change tracker; rows added earlier in this scope would still be
    // tracked under the same keys and block adding them again.
    private void ForgetTracked(Guid storeId)
    {
        foreach (var entry in db.ChangeTracker.Entries<StoreSectionOrder>().Where(e => e.Entity.StoreId == storeId).ToList())
        {
            entry.State = EntityState.Detached;
        }
    }
}
