using FoodAssistant.Application.Households;
using FoodAssistant.Application.Shopping;
using FoodAssistant.Domain.Quantities;
using FoodAssistant.Infrastructure.Catalog;
using FoodAssistant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace FoodAssistant.Infrastructure.Shopping;

/// <summary>Shopping list entries; the active-household filter applies to every query.</summary>
internal sealed class ShoppingItemStore(AppDbContext db, IHouseholdContext householdContext) : IShoppingItemStore
{
    private DbSet<ShoppingItem> Items => db.Set<ShoppingItem>();

    public async Task<IReadOnlyList<ShoppingItemRecord>> ListItemsAsync(
        Guid listId,
        bool includeChecked,
        CancellationToken cancellationToken)
    {
        var items = await Items
            .AsNoTracking()
            .Where(i => i.ListId == listId && (includeChecked || !i.Checked))
            .OrderBy(i => i.CreatedAt)
            .ThenBy(i => i.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return [.. items.Select(ToRecord)];
    }

    public async Task<ShoppingItemRecord?> GetItemAsync(Guid itemId, bool includeDeleted, CancellationToken cancellationToken)
    {
        var query = Items.AsNoTracking();
        if (includeDeleted)
        {
            // Only the tombstone filter is lifted (the conflict rules need deleted entries); the
            // active-household filter stays, so another household's entry is still invisible.
            query = query.IgnoreQueryFilters([AppDbContext.NotDeletedFilter]);
        }

        var item = await query.SingleOrDefaultAsync(i => i.Id == itemId, cancellationToken).ConfigureAwait(false);
        return item is null ? null : ToRecord(item);
    }

    public async Task<ShoppingItemRecord?> AddItemAsync(
        Guid listId,
        NewShoppingItem item,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(item);
        var entity = new ShoppingItem
        {
            Id = item.Id,
            HouseholdId = householdContext.HouseholdId
                ?? throw new InvalidOperationException("Shopping items need an active household."),
            ListId = listId,
            ProductId = item.ProductId,
            Text = item.Text,
            QuantityValue = item.Quantity?.Value,
            QuantityUnitCode = item.Quantity?.Unit.Code,
            Note = item.Note,
            SectionKey = item.SectionKey,
            Origin = item.Origin,
            CreatedByUserId = item.CreatedByUserId,
            Version = 1,
            CreatedAt = now,
            UpdatedAt = now,
        };
        Items.Add(entity);
        try
        {
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException exception) when (IsKeyConflict(exception))
        {
            // Id taken by a concurrent insert (or another household), or the list was deleted meanwhile.
            db.ChangeTracker.Clear();
            return null;
        }

        return ToRecord(entity);
    }

    public async Task<bool> ApplyItemAsync(ShoppingItemRecord updated, long expectedVersion, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(updated);

        // Tombstones too: a change may win against a deletion and bring the entry back.
        var entity = await Items
            .IgnoreQueryFilters([AppDbContext.NotDeletedFilter])
            .SingleOrDefaultAsync(i => i.Id == updated.Id, cancellationToken)
            .ConfigureAwait(false);
        if (entity is null || entity.Version != expectedVersion)
        {
            db.ChangeTracker.Clear();
            return false;
        }

        entity.ListId = updated.ListId;
        entity.Text = updated.Text;
        entity.QuantityValue = updated.Quantity?.Value;
        entity.QuantityUnitCode = updated.Quantity?.Unit.Code;
        entity.Note = updated.Note;
        entity.SectionKey = updated.SectionKey;
        entity.Checked = updated.Checked;
        entity.CheckedAt = updated.CheckedAt;
        entity.CheckedByUserId = updated.CheckedByUserId;
        entity.Version = updated.Version;
        entity.UpdatedAt = updated.UpdatedAt;
        entity.DeletedAt = updated.DeletedAt;

        // Version is a concurrency token: the UPDATE only matches while the row still has the
        // version the decision was based on; a concurrent change makes it fail instead of being lost.
        db.Entry(entity).Property(i => i.Version).OriginalValue = expectedVersion;
        try
        {
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException)
        {
            db.ChangeTracker.Clear();
            return false;
        }
        catch (DbUpdateException exception) when (IsKeyConflict(exception))
        {
            // Moved to a list that was deleted meanwhile; the retry reports it as not found.
            db.ChangeTracker.Clear();
            return false;
        }

        return true;
    }

    public Task SavePendingAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);

    public async Task<(string Name, string CategoryKey, string? SectionKey)?> GetProductForItemAsync(
        Guid productId,
        CancellationToken cancellationToken)
    {
        var product = await db.Set<Product>()
            .AsNoTracking()
            .Where(p => p.Id == productId && p.ArchivedAt == null)
            .Select(p => new { p.Name, p.CategoryKey, p.SectionKey })
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        return product is null ? null : (product.Name, product.CategoryKey, product.SectionKey);
    }

    private static bool IsKeyConflict(DbUpdateException exception) =>
        PostgresErrors.IsUniqueViolation(exception)
        || exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.ForeignKeyViolation };

    private static ShoppingItemRecord ToRecord(ShoppingItem item) => new(
        item.Id,
        item.ListId,
        item.ProductId,
        item.Text,
        item is { QuantityValue: { } value, QuantityUnitCode: { } unit } ? new Quantity(value, Unit.FromCode(unit)) : null,
        item.Note,
        item.SectionKey,
        item.Origin,
        item.Checked,
        item.CheckedAt,
        item.CheckedByUserId,
        item.Version,
        item.UpdatedAt,
        item.DeletedAt);
}
