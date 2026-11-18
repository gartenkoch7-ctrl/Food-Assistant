using FoodAssistant.Application.Activity;
using FoodAssistant.Application.Checkout;
using FoodAssistant.Application.Common;
using FoodAssistant.Application.Households;
using FoodAssistant.Contracts.Checkout;
using FoodAssistant.Contracts.Common;
using FoodAssistant.Domain.Checkout;
using FoodAssistant.Domain.Expiry;
using FoodAssistant.Domain.Inventory;
using FoodAssistant.Domain.Quantities;
using FoodAssistant.Infrastructure.Catalog;
using FoodAssistant.Infrastructure.Inventory;
using FoodAssistant.Infrastructure.Persistence;
using FoodAssistant.Infrastructure.Shopping;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace FoodAssistant.Infrastructure.Checkout;

/// <summary>
/// Completing a shopping list through EF Core; the active-household filter applies to every read.
/// The commit runs in one explicit transaction that takes the household's change-log lock first
/// (docs/kb/datenmodell.md): every other writer of the household's entries, products and locations
/// waits for it, so the checks made under the lock still hold at the commit.
/// </summary>
internal sealed class CheckoutStore(AppDbContext db, IHouseholdContext householdContext, IActivityLog activityLog) : ICheckoutStore
{
    public async Task<CheckoutData?> LoadAsync(
        Guid listId,
        IReadOnlyCollection<Guid> extraProductIds,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(extraProductIds);
        if (!await db.Set<ShoppingList>().AnyAsync(l => l.Id == listId, cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        var items = await db.Set<ShoppingItem>()
            .AsNoTracking()
            .Where(i => i.ListId == listId && i.Checked)
            .OrderBy(i => i.CreatedAt)
            .ThenBy(i => i.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var productIds = items.Select(i => i.ProductId).OfType<Guid>().Concat(extraProductIds).Distinct().ToList();
        var products = await LoadProductsAsync(productIds, cancellationToken).ConfigureAwait(false);
        var locations = await db.Set<StorageLocation>()
            .AsNoTracking()
            .OrderBy(l => l.SortOrder).ThenBy(l => l.Name).ThenBy(l => l.Id)
            .Select(l => new { l.Id, l.Type })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return new CheckoutData(
            listId,
            [.. items.Select(ToItem)],
            products,
            [.. locations.Select(l => new LocationOption(l.Id, l.Type))]);
    }

    public async Task<OperationResult<CheckoutCommitted>> CommitAsync(CheckoutCommit commit, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(commit);
        var householdId = householdContext.HouseholdId
            ?? throw new InvalidOperationException("Checkout needs an active household.");
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        // Change-log lock first, like every implicit save of a synchronised entity: the lock order
        // rules out a deadlock, and changes of other requests wait, so the checks below stay true.
        await PostgresLocks.LockHouseholdChangeLogAsync(transaction.GetDbTransaction(), householdId, cancellationToken)
            .ConfigureAwait(false);
        if (!await db.Set<ShoppingList>().AnyAsync(l => l.Id == commit.ListId, cancellationToken).ConfigureAwait(false))
        {
            return OperationResult<CheckoutCommitted>.NotFound(CheckoutErrorCodes.ListNotFound);
        }

        if (!await LocationsUnchangedAsync(commit, cancellationToken).ConfigureAwait(false))
        {
            return OperationResult<CheckoutCommitted>.Conflict(ErrorCodes.Conflict);
        }

        var itemIds = commit.Lines.Select(l => l.ItemId).ToList();
        var items = await db.Set<ShoppingItem>()
            .Where(i => itemIds.Contains(i.Id))
            .ToDictionaryAsync(i => i.Id, cancellationToken)
            .ConfigureAwait(false);
        var productIds = commit.Lines.Select(l => l.Batch).OfType<CheckoutBatch>()
            .Where(b => b.NewProduct is null).Select(b => b.ProductId).Distinct().ToList();
        var products = await db.Set<Product>()
            .Where(p => productIds.Contains(p.Id) && p.ArchivedAt == null)
            .ToDictionaryAsync(p => p.Id, cancellationToken)
            .ConfigureAwait(false);

        var applied = new List<CheckoutCommitLine>(commit.Lines.Count);
        var stale = new List<Guid>();
        foreach (var line in commit.Lines)
        {
            // Deleted entries are hidden by the tombstone filter and count as changed.
            if (!items.TryGetValue(line.ItemId, out var item)
                || item.ListId != commit.ListId
                || !item.Checked
                || item.Version != line.ExpectedVersion
                || (line.Batch is { } batch && !AddBatch(batch, products, householdId, commit)))
            {
                stale.Add(line.ItemId);
                continue;
            }

            // Version is the concurrency token and was loaded under the lock; the application counts it up.
            item.DeletedAt = commit.Now;
            item.UpdatedAt = commit.Now;
            item.Version++;
            applied.Add(line);
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException)
        {
            db.ChangeTracker.Clear();
            return OperationResult<CheckoutCommitted>.Conflict(ErrorCodes.Conflict);
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.ForeignKeyViolation })
        {
            // A location was deleted between reading and this transaction; nothing is stored.
            db.ChangeTracker.Clear();
            return OperationResult<CheckoutCommitted>.Conflict(ErrorCodes.Conflict);
        }

        return OperationResult<CheckoutCommitted>.Ok(new CheckoutCommitted(applied, stale));
    }

    // The batches were built for the storage type read before the lock (frozen at home or sealed);
    // a location deleted or switched between fridge and freezer since then would contradict them.
    private async Task<bool> LocationsUnchangedAsync(CheckoutCommit commit, CancellationToken cancellationToken)
    {
        var batches = commit.Lines.Select(l => l.Batch).OfType<CheckoutBatch>().ToList();
        var locationIds = batches.Select(b => b.StorageLocationId).Distinct().ToList();
        var types = await db.Set<StorageLocation>()
            .Where(l => locationIds.Contains(l.Id))
            .ToDictionaryAsync(l => l.Id, l => l.Type, cancellationToken)
            .ConfigureAwait(false);
        return batches.All(b => types.TryGetValue(b.StorageLocationId, out var type) && type == b.Batch.StorageType);
    }

    // Adds the batch (and its new product); false if its product was archived meanwhile.
    private bool AddBatch(CheckoutBatch batch, Dictionary<Guid, Product> products, Guid householdId, CheckoutCommit commit)
    {
        Product? product;
        if (batch.NewProduct is { } created)
        {
            product = new Product
            {
                Id = created.Id,
                HouseholdId = householdId,
                Name = created.Name,
                CategoryKey = created.CategoryKey,
                DefaultUnitCode = created.DefaultUnitCode,
                CreatedAt = commit.Now,
                UpdatedAt = commit.Now,
            };
            db.Add(product);
        }
        else if (!products.TryGetValue(batch.ProductId, out product))
        {
            return false;
        }

        db.Add(StockBatchMapping.NewRecord(
            batch.Batch, householdId, batch.ProductId, batch.StorageLocationId, BatchOrigin.Purchase, commit.Today, commit.Now));
        if (batch.Remember is { } remember)
        {
            product.LastShelfLifeDays = remember.Days;
            product.LastExpiryKind = remember.Kind;
            product.UpdatedAt = commit.Now;
        }

        activityLog.Add(batch.Activity);
        return true;
    }

    private async Task<Dictionary<Guid, CheckoutProduct>> LoadProductsAsync(List<Guid> productIds, CancellationToken cancellationToken)
    {
        var products = await db.Set<Product>()
            .AsNoTracking()
            .Where(p => productIds.Contains(p.Id) && p.ArchivedAt == null)
            .Select(p => new { p.Id, p.CategoryKey, p.DefaultUnitCode, p.DefaultStorageLocationId, p.LastShelfLifeDays, p.LastExpiryKind })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // Batches are few per product; picking the newest in memory keeps the query simple.
        var batches = await db.Set<StockBatchRecord>()
            .AsNoTracking()
            .Where(b => productIds.Contains(b.ProductId))
            .Select(b => new { b.Id, b.ProductId, b.StorageLocationId, b.CreatedAt })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var lastLocation = batches
            .GroupBy(b => b.ProductId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(b => b.CreatedAt).ThenByDescending(b => b.Id).First().StorageLocationId);

        return products.ToDictionary(
            p => p.Id,
            p => new CheckoutProduct(
                p.Id,
                p.CategoryKey,
                p.DefaultUnitCode,
                p.DefaultStorageLocationId,
                lastLocation.TryGetValue(p.Id, out var location) ? location : null,
                // Only values the domain would suggest count; anything else (e.g. imported data) is "unknown",
                // so a freezer line still gets the rule "frozen".
                p is { LastShelfLifeDays: { } days and >= 0 and <= PurchasedBatch.MaxRememberedDays, LastExpiryKind: { } kind }
                    && kind != ExpiryKind.None
                    ? new LastPurchase(days, kind)
                    : null));
    }

    private static CheckoutItem ToItem(ShoppingItem item) => new(
        item.Id,
        item.Version,
        item.ProductId,
        item.Text,
        item is { QuantityValue: { } value, QuantityUnitCode: { } unit } ? new Quantity(value, Unit.FromCode(unit)) : null);
}
