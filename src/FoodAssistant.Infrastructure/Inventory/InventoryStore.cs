using FoodAssistant.Application.Activity;
using FoodAssistant.Application.Households;
using FoodAssistant.Application.Inventory;
using FoodAssistant.Contracts.Catalog;
using FoodAssistant.Infrastructure.Activity;
using FoodAssistant.Infrastructure.Catalog;
using FoodAssistant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace FoodAssistant.Infrastructure.Inventory;

/// <summary>
/// Batches, consumption events and inventory activities through EF Core; the active-household
/// query filter applies to every read. Batches and activities are loaded tracked, so the version
/// read then guards the save (store pattern, docs/kb/datenmodell.md).
/// </summary>
internal sealed class InventoryStore(AppDbContext db, IHouseholdContext householdContext) : IInventoryStore
{
    public async Task<IReadOnlyList<ProductBatches>> ListAsync(CancellationToken cancellationToken)
    {
        var records = await db.Set<StockBatchRecord>().AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false);
        var productIds = records.Select(r => r.ProductId).Distinct().ToList();

        // Archived products have no batches (archiving requires an empty stock), so no archive filter here.
        var products = await db.Set<Product>()
            .AsNoTracking()
            .Where(p => productIds.Contains(p.Id))
            .Select(p => new ProductSummaryDto(p.Id, p.Name, p.Brand, p.CategoryKey, p.DefaultUnitCode))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var batchesByProduct = records.ToLookup(r => r.ProductId);
        return [.. products.Select(p => new ProductBatches(p, [.. batchesByProduct[p.Id].Select(ToContext)]))];
    }

    public async Task<BatchContext?> GetBatchAsync(Guid batchId, CancellationToken cancellationToken)
    {
        var record = await db.Set<StockBatchRecord>()
            .SingleOrDefaultAsync(b => b.Id == batchId, cancellationToken)
            .ConfigureAwait(false);
        return record is null ? null : ToContext(record);
    }

    public async Task<IReadOnlyList<BatchContext>> GetBatchesOfProductAsync(Guid productId, CancellationToken cancellationToken)
    {
        var records = await db.Set<StockBatchRecord>()
            .Where(b => b.ProductId == productId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return [.. records.Select(ToContext)];
    }

    public Task<ProductSummaryDto?> GetProductAsync(Guid productId, CancellationToken cancellationToken) =>
        db.Set<Product>()
            .AsNoTracking()
            .Where(p => p.Id == productId && p.ArchivedAt == null)
            .Select(p => new ProductSummaryDto(p.Id, p.Name, p.Brand, p.CategoryKey, p.DefaultUnitCode))
            .SingleOrDefaultAsync(cancellationToken);

    public Task<StorageLocationInfo?> GetLocationAsync(Guid locationId, CancellationToken cancellationToken) =>
        db.Set<StorageLocation>()
            .AsNoTracking()
            .Where(l => l.Id == locationId)
            .Select(l => new StorageLocationInfo(l.Id, l.Type))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<InventoryActivity?> GetActivityAsync(Guid activityId, CancellationToken cancellationToken)
    {
        var activity = await db.Set<HouseholdActivity>()
            .SingleOrDefaultAsync(a => a.Id == activityId && a.Area == ActivityArea.Inventory, cancellationToken)
            .ConfigureAwait(false);
        return activity is null
            ? null
            : new InventoryActivity(activity.Id, activity.Kind, activity.ProductId, activity.PayloadJson, activity.OccurredAt, activity.UndoneAt);
    }

    public async Task<BatchSaveResult> SaveAsync(BatchChange change, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(change);
        var householdId = householdContext.HouseholdId
            ?? throw new InvalidOperationException("Inventory changes need an active household.");

        var written = new List<StockBatchRecord>();
        foreach (var update in change.Updated)
        {
            var record = Loaded(update.Batch.Id, update.ExpectedVersion);
            record.Apply(update.Batch, change.Now);
            record.StorageLocationId = update.StorageLocationId;
            written.Add(record);
        }

        foreach (var added in change.Added)
        {
            var record = StockBatchMapping.NewRecord(
                added.Batch, householdId, added.ProductId, added.StorageLocationId, added.Origin, added.AddedOn, change.Now);
            db.Add(record);
            written.Add(record);
        }

        foreach (var removal in change.Removed)
        {
            db.Remove(Loaded(removal.BatchId, removal.ExpectedVersion));
        }

        foreach (var consumption in change.Events)
        {
            db.Add(InventoryEventMapping.ToEntity(consumption, householdId, change.Now));
        }

        await MarkUndoneAsync(change, cancellationToken).ConfigureAwait(false);

        var status = await TrySaveAsync(cancellationToken).ConfigureAwait(false);
        return status == BatchSaveStatus.Saved
            ? new BatchSaveResult(status, written.ToDictionary(r => r.Id, r => (long)r.Version))
            : new BatchSaveResult(status, new Dictionary<Guid, long>());
    }

    private async Task MarkUndoneAsync(BatchChange change, CancellationToken cancellationToken)
    {
        if (change.UndoneEventIds.Count > 0)
        {
            var ids = change.UndoneEventIds.ToList();
            var events = await db.Set<ConsumptionEvent>()
                .Where(e => ids.Contains(e.Id))
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
            foreach (var consumption in events)
            {
                consumption.UndoneAt = change.Now;
            }
        }

        if (change.UndoneActivityId is { } activityId)
        {
            var activity = db.Set<HouseholdActivity>().Local.FirstOrDefault(a => a.Id == activityId)
                ?? throw new InvalidOperationException("The activity must be loaded through this store first.");
            activity.UndoneAt = change.Now;
            activity.UpdatedAt = change.Now;
        }
    }

    private async Task<BatchSaveStatus> TrySaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return BatchSaveStatus.Saved;
        }
        catch (DbUpdateConcurrencyException)
        {
            // Changed or deleted since loading (another request, or undone twice at once).
            db.ChangeTracker.Clear();
            return BatchSaveStatus.Conflict;
        }
        catch (DbUpdateException exception) when (PostgresErrors.IsUniqueViolation(exception))
        {
            db.ChangeTracker.Clear();
            return BatchSaveStatus.Duplicate;
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.ForeignKeyViolation })
        {
            // Product or location removed in the meantime (e.g. a location deleted before an undo).
            db.ChangeTracker.Clear();
            return BatchSaveStatus.Conflict;
        }
    }

    // Store pattern step 3: the expected version is the original value, so a race fails the save.
    private StockBatchRecord Loaded(Guid batchId, long expectedVersion)
    {
        var record = db.Set<StockBatchRecord>().Local.FirstOrDefault(r => r.Id == batchId)
            ?? throw new InvalidOperationException("The batch must be loaded through this store first.");
        db.Entry(record).Property(r => r.Version).OriginalValue = (uint)expectedVersion;
        return record;
    }

    private static BatchContext ToContext(StockBatchRecord record) =>
        new(record.ToDomain(), record.ProductId, record.StorageLocationId, record.Origin, record.AddedOn, record.UpdatedAt, record.Version);
}
