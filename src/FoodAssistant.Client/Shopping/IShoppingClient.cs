using FoodAssistant.Client.Http;
using FoodAssistant.Contracts.Shopping;

namespace FoodAssistant.Client.Shopping;

/// <summary>
/// Shopping lists and their items of one household. This interface is the offline seam: the mobile
/// app (M2) wraps it with a cache and a change queue, so the UI never talks to HTTP directly.
/// Items are changed with their full target state plus the base version they were read at (no
/// ETag; the server resolves conflicts, see ADR-0005); lists use ETags like the other resources.
/// </summary>
public interface IShoppingClient
{
    /// <summary>The server creates the unnamed default list when the household has none yet.</summary>
    Task<ApiResult<IReadOnlyList<ShoppingListDto>>> ListListsAsync(Guid householdId, CancellationToken cancellationToken);

    Task<ApiResult<ShoppingListDto>> CreateListAsync(Guid householdId, CreateShoppingListRequest request, CancellationToken cancellationToken);

    Task<ApiResult<ShoppingListDto>> UpdateListAsync(
        Guid householdId, Guid listId, UpdateShoppingListRequest request, string etag, CancellationToken cancellationToken);

    Task<ApiResult> DeleteListAsync(Guid householdId, Guid listId, string etag, CancellationToken cancellationToken);

    Task<ApiResult<IReadOnlyList<ShoppingItemDto>>> ListItemsAsync(
        Guid householdId, Guid listId, bool includeChecked, CancellationToken cancellationToken);

    /// <summary>Adds an item; sending a known <c>Id</c> again returns the existing item (safe to retry).</summary>
    Task<ApiResult<ShoppingItemDto>> AddItemAsync(
        Guid householdId, Guid listId, CreateShoppingItemRequest request, CancellationToken cancellationToken);

    Task<ApiResult<ShoppingItemChangeResult>> ChangeItemAsync(
        Guid householdId, Guid itemId, ChangeShoppingItemRequest request, CancellationToken cancellationToken);

    Task<ApiResult<ShoppingItemChangeResult>> DeleteItemAsync(
        Guid householdId, Guid itemId, long baseVersion, CancellationToken cancellationToken);

    Task<ApiResult<ShoppingItemDto>> MoveItemAsync(
        Guid householdId, Guid itemId, MoveShoppingItemRequest request, CancellationToken cancellationToken);
}
