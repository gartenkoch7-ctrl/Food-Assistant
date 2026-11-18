using FoodAssistant.Application.Common;
using FoodAssistant.Application.Households;
using FoodAssistant.Contracts.Shopping;
using FoodAssistant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace FoodAssistant.Infrastructure.Shopping;

/// <summary>
/// Deletes a shopping list with its entries. The database cascade <c>ShoppingItems.ListId</c> would
/// bypass the change log (docs/kb/datenmodell.md), so every entry is deleted through the context.
/// </summary>
internal sealed class ShoppingListRemover(AppDbContext db, IHouseholdContext householdContext, ShoppingListReader reader)
{
    public async Task<OperationResult<ShoppingListDto>> DeleteAsync(
        Guid listId,
        long? expectedVersion,
        CancellationToken cancellationToken)
    {
        var householdId = householdContext.HouseholdId
            ?? throw new InvalidOperationException("Shopping lists need an active household.");
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        // Change-log lock first, like every implicit save of a synchronised entity: it keeps other
        // writers of the household (new or moved entries) out until the commit, so none can slip
        // past the change log through the cascade, and the lock order rules out a deadlock.
        await PostgresLocks.LockHouseholdChangeLogAsync(transaction.GetDbTransaction(), householdId, cancellationToken)
            .ConfigureAwait(false);
        await db.Database.ExecuteSqlAsync(
                $"SELECT 1 FROM \"ShoppingLists\" WHERE \"Id\" = {listId} AND \"HouseholdId\" = {householdId} FOR UPDATE",
                cancellationToken)
            .ConfigureAwait(false);
        var list = await db.Set<ShoppingList>().SingleOrDefaultAsync(l => l.Id == listId, cancellationToken).ConfigureAwait(false);
        if (list is null)
        {
            return OperationResult<ShoppingListDto>.NotFound(ShoppingErrorCodes.ListNotFound);
        }

        if (expectedVersion is { } expected && expected != list.Version)
        {
            return OperationResult<ShoppingListDto>.VersionMismatch(
                list.Version,
                await reader.ToDtoAsync(list, cancellationToken).ConfigureAwait(false));
        }

        if (list.IsDefault)
        {
            return OperationResult<ShoppingListDto>.Conflict(ShoppingErrorCodes.DefaultList);
        }

        // Tombstones too (only the NotDeleted filter is lifted; the household filter stays).
        var items = await db.Set<ShoppingItem>()
            .IgnoreQueryFilters([AppDbContext.NotDeletedFilter])
            .Where(i => i.ListId == listId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        db.RemoveRange(items);
        db.Remove(list);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return OperationResult<ShoppingListDto>.Deleted();
    }
}
