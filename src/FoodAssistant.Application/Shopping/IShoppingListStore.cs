using FoodAssistant.Application.Common;
using FoodAssistant.Contracts.Shopping;

namespace FoodAssistant.Application.Shopping;

/// <summary>Shopping lists of the active household (household query filter applies everywhere).</summary>
public interface IShoppingListStore
{
    /// <summary>All lists with item counts, ordered by <see cref="ShoppingListDto.SortOrder"/>.</summary>
    Task<IReadOnlyList<ShoppingListDto>> ListListsAsync(CancellationToken cancellationToken);

    Task<ShoppingListDto?> GetListAsync(Guid listId, CancellationToken cancellationToken);

    Task<bool> ListExistsAsync(Guid listId, CancellationToken cancellationToken);

    /// <summary>Adds a list at the end; <c>Conflict(common.conflict)</c> if the id is taken.</summary>
    Task<OperationResult<ShoppingListDto>> AddListAsync(
        Guid id,
        string name,
        Guid? storeId,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    /// <summary>
    /// Changes a list (store pattern, docs/kb/datenmodell.md). Making it the default resets the
    /// previous default in the same transaction; un-defaulting the default list is
    /// <c>Conflict(shopping.default_list)</c>.
    /// </summary>
    Task<OperationResult<ShoppingListDto>> UpdateListAsync(
        Guid listId,
        long? expectedVersion,
        UpdateShoppingListRequest changes,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    /// <summary>
    /// Deletes a non-default list together with its entries (tombstones included), each through the
    /// context so the change log records them (<c>Conflict(shopping.default_list)</c> for the default list).
    /// </summary>
    Task<OperationResult<ShoppingListDto>> DeleteListAsync(
        Guid listId,
        long? expectedVersion,
        CancellationToken cancellationToken);

    /// <summary>Returns the default list, creating an unnamed one if the household has none (safe under concurrency).</summary>
    Task<ShoppingListDto> EnsureDefaultListAsync(DateTimeOffset now, CancellationToken cancellationToken);

    Task<bool> StoreExistsAsync(Guid storeId, CancellationToken cancellationToken);

    /// <summary>Store of the list, or <see langword="null"/> if it has none or does not exist.</summary>
    Task<Guid?> GetStoreIdOfListAsync(Guid listId, CancellationToken cancellationToken);
}
