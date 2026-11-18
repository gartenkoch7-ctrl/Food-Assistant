using FoodAssistant.Application.Restock;
using FoodAssistant.Domain.Quantities;
using FoodAssistant.Infrastructure.Catalog;
using FoodAssistant.Infrastructure.Inventory;
using FoodAssistant.Infrastructure.Persistence;
using FoodAssistant.Infrastructure.Shopping;
using Microsoft.EntityFrameworkCore;

namespace FoodAssistant.Infrastructure.Restock;

/// <summary>Minimum-stock state of products; the active-household filter applies to every query.</summary>
internal sealed class RestockStore(AppDbContext db) : IRestockStore
{
    public async Task<ProductStockState?> GetProductStockAsync(Guid productId, CancellationToken cancellationToken)
    {
        var product = await db.Set<Product>()
            .AsNoTracking()
            .SingleOrDefaultAsync(p => p.Id == productId && p.ArchivedAt == null, cancellationToken)
            .ConfigureAwait(false);
        if (product is null)
        {
            return null;
        }

        var records = await db.Set<StockBatchRecord>()
            .AsNoTracking()
            .Where(b => b.ProductId == productId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // Tombstones are filtered out by the query filter: a deleted entry no longer counts as "on the list".
        var onOpenList = await db.Set<ShoppingItem>()
            .AnyAsync(i => i.ProductId == productId && !i.Checked, cancellationToken)
            .ConfigureAwait(false);

        Quantity? minimum = product is { MinimumStockValue: { } value, MinimumStockUnitCode: { } code }
            ? new Quantity(value, Unit.FromCode(code))
            : null;
        return new ProductStockState(
            product.Name,
            product.CategoryKey,
            product.SectionKey,
            minimum,
            product.MinimumStockTriggered,
            [.. records.Select(r => r.ToDomain())],
            onOpenList);
    }

    public async Task<bool> SetTriggeredAsync(Guid productId, bool triggered, CancellationToken cancellationToken)
    {
        // A failed insert of a list entry may have left it tracked; saving again would retry it.
        db.ChangeTracker.Clear();

        // Tracked and saved through the context: the product is synchronised (change log), so no ExecuteUpdate.
        var product = await db.Set<Product>()
            .SingleOrDefaultAsync(p => p.Id == productId, cancellationToken)
            .ConfigureAwait(false);
        if (product is null || product.MinimumStockTriggered == triggered)
        {
            return false;
        }

        product.MinimumStockTriggered = triggered;
        try
        {
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Someone changed the product between reading and saving (xmin): they won the claim.
            db.ChangeTracker.Clear();
            return false;
        }

        return true;
    }
}
