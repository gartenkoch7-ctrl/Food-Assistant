using System.Globalization;
using FoodAssistant.Client.Http;
using FoodAssistant.Contracts.Shopping;

namespace FoodAssistant.Client.Shopping;

/// <summary>The shopping API of a household over <see cref="ApiHttpClient"/>.</summary>
public sealed class ShoppingClient(ApiHttpClient api) : IShoppingClient
{
    public Task<ApiResult<IReadOnlyList<ShoppingListDto>>> ListListsAsync(Guid householdId, CancellationToken cancellationToken) =>
        api.GetAsync<IReadOnlyList<ShoppingListDto>>(ApiPaths.Household(householdId, "shopping/lists"), cancellationToken);

    public Task<ApiResult<ShoppingListDto>> CreateListAsync(
        Guid householdId, CreateShoppingListRequest request, CancellationToken cancellationToken) =>
        api.SendAsync<CreateShoppingListRequest, ShoppingListDto>(
            HttpMethod.Post, ApiPaths.Household(householdId, "shopping/lists"), request, null, null, cancellationToken);

    public Task<ApiResult<ShoppingListDto>> UpdateListAsync(
        Guid householdId, Guid listId, UpdateShoppingListRequest request, string etag, CancellationToken cancellationToken) =>
        api.SendAsync<UpdateShoppingListRequest, ShoppingListDto>(
            HttpMethod.Put, ApiPaths.Household(householdId, $"shopping/lists/{listId:D}"), request, etag, null, cancellationToken);

    public Task<ApiResult> DeleteListAsync(Guid householdId, Guid listId, string etag, CancellationToken cancellationToken) =>
        api.SendAsync<object>(
            HttpMethod.Delete, ApiPaths.Household(householdId, $"shopping/lists/{listId:D}"), null, etag, null, cancellationToken);

    public Task<ApiResult<IReadOnlyList<ShoppingItemDto>>> ListItemsAsync(
        Guid householdId, Guid listId, bool includeChecked, CancellationToken cancellationToken) =>
        api.GetAsync<IReadOnlyList<ShoppingItemDto>>(
            ApiPaths.Household(householdId, $"shopping/lists/{listId:D}/items?includeChecked={(includeChecked ? "true" : "false")}"),
            cancellationToken);

    public Task<ApiResult<ShoppingItemDto>> AddItemAsync(
        Guid householdId, Guid listId, CreateShoppingItemRequest request, CancellationToken cancellationToken) =>
        api.SendAsync<CreateShoppingItemRequest, ShoppingItemDto>(
            HttpMethod.Post, ApiPaths.Household(householdId, $"shopping/lists/{listId:D}/items"), request, null, null, cancellationToken);

    public Task<ApiResult<ShoppingItemChangeResult>> ChangeItemAsync(
        Guid householdId, Guid itemId, ChangeShoppingItemRequest request, CancellationToken cancellationToken) =>
        api.SendAsync<ChangeShoppingItemRequest, ShoppingItemChangeResult>(
            HttpMethod.Put, ApiPaths.Household(householdId, $"shopping/items/{itemId:D}"), request, null, null, cancellationToken);

    public Task<ApiResult<ShoppingItemChangeResult>> DeleteItemAsync(
        Guid householdId, Guid itemId, long baseVersion, CancellationToken cancellationToken) =>
        api.SendAsync<object, ShoppingItemChangeResult>(
            HttpMethod.Delete,
            ApiPaths.Household(householdId, $"shopping/items/{itemId:D}?baseVersion={baseVersion.ToString(CultureInfo.InvariantCulture)}"),
            null, null, null, cancellationToken);

    public Task<ApiResult<ShoppingItemDto>> MoveItemAsync(
        Guid householdId, Guid itemId, MoveShoppingItemRequest request, CancellationToken cancellationToken) =>
        api.SendAsync<MoveShoppingItemRequest, ShoppingItemDto>(
            HttpMethod.Post, ApiPaths.Household(householdId, $"shopping/items/{itemId:D}/move"), request, null, null, cancellationToken);
}
