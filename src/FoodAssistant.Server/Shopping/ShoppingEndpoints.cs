using FoodAssistant.Application.Households;
using FoodAssistant.Application.Shopping;
using FoodAssistant.Contracts.Shopping;
using FoodAssistant.Server.Api.Concurrency;
using FoodAssistant.Server.Api.Results;
using FoodAssistant.Server.Auth;
using FoodAssistant.Server.Households;

namespace FoodAssistant.Server.Shopping;

/// <summary>
/// <c>/api/v1/households/{householdId}/shopping</c>: lists (If-Match) and entries. Entries are
/// changed with their full target state and the version it was based on instead of If-Match,
/// because offline changes are merged by the conflict rules of ADR-0005 (exception in ADR-0004).
/// </summary>
internal static class ShoppingEndpoints
{
    public static RouteGroupBuilder MapShoppingEndpoints(this RouteGroupBuilder household)
    {
        ArgumentNullException.ThrowIfNull(household);
        var shopping = household.MapGroup("/shopping").WithTags("Shopping");

        shopping.MapGet("/lists", ListListsAsync)
            .RequireAuthorization(AuthPolicies.HouseholdAny)
            .Produces<IReadOnlyList<ShoppingListDto>>();
        shopping.MapPost("/lists", CreateListAsync)
            .RequireAuthorization(AuthPolicies.HouseholdMember)
            .Produces<ShoppingListDto>(StatusCodes.Status201Created);
        shopping.MapPut("/lists/{listId:guid}", UpdateListAsync)
            .RequireAuthorization(AuthPolicies.HouseholdMember)
            .Produces<ShoppingListDto>();
        shopping.MapDelete("/lists/{listId:guid}", DeleteListAsync)
            .RequireAuthorization(AuthPolicies.HouseholdMember)
            .Produces(StatusCodes.Status204NoContent);

        shopping.MapGet("/lists/{listId:guid}/items", ListItemsAsync)
            .RequireAuthorization(AuthPolicies.HouseholdAny)
            .Produces<IReadOnlyList<ShoppingItemDto>>();

        // Restricted members may add and check off (ADR-0002); the service checks the change itself.
        shopping.MapPost("/lists/{listId:guid}/items", AddItemAsync)
            .RequireAuthorization(AuthPolicies.HouseholdAny)
            .Produces<ShoppingItemDto>(StatusCodes.Status201Created)
            .Produces<ShoppingItemDto>();
        shopping.MapPut("/items/{itemId:guid}", ChangeItemAsync)
            .RequireAuthorization(AuthPolicies.HouseholdAny)
            .Produces<ShoppingItemChangeResult>();
        shopping.MapDelete("/items/{itemId:guid}", DeleteItemAsync)
            .RequireAuthorization(AuthPolicies.HouseholdMember)
            .Produces<ShoppingItemChangeResult>();
        shopping.MapPost("/items/{itemId:guid}/move", MoveItemAsync)
            .RequireAuthorization(AuthPolicies.HouseholdMember)
            .Produces<ShoppingItemDto>();
        return household;
    }

    private static async Task<IResult> ListListsAsync(
        Guid householdId,
        ShoppingListService lists,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await lists.ListAsync(householdId, cancellationToken).ConfigureAwait(false));

    private static async Task<IResult> CreateListAsync(
        Guid householdId,
        CreateShoppingListRequest request,
        HttpContext http,
        ShoppingListService lists,
        CancellationToken cancellationToken)
    {
        var result = await lists.CreateAsync(householdId, request, cancellationToken).ConfigureAwait(false);
        return result.ToHttpResult(http, list => list.Version);
    }

    private static async Task<IResult> UpdateListAsync(
        Guid householdId,
        Guid listId,
        UpdateShoppingListRequest request,
        HttpContext http,
        ShoppingListService lists,
        CancellationToken cancellationToken)
    {
        if (!IfMatch.TryRead(http, required: true, out var expected, out var problem))
        {
            return problem!;
        }

        var result = await lists.UpdateAsync(householdId, listId, expected, request, cancellationToken).ConfigureAwait(false);
        return result.ToHttpResult(http, list => list.Version);
    }

    private static async Task<IResult> DeleteListAsync(
        Guid householdId,
        Guid listId,
        HttpContext http,
        ShoppingListService lists,
        CancellationToken cancellationToken)
    {
        if (!IfMatch.TryRead(http, required: true, out var expected, out var problem))
        {
            return problem!;
        }

        var result = await lists.DeleteAsync(householdId, listId, expected, cancellationToken).ConfigureAwait(false);
        return result.ToHttpResult(http, list => list.Version);
    }

    private static async Task<IResult> ListItemsAsync(
        Guid householdId,
        Guid listId,
        bool? includeChecked,
        HttpContext http,
        ShoppingItemService items,
        CancellationToken cancellationToken)
    {
        var result = await items.ListAsync(householdId, listId, includeChecked ?? true, cancellationToken).ConfigureAwait(false);
        return result.ToHttpResult(http);
    }

    private static async Task<IResult> AddItemAsync(
        Guid householdId,
        Guid listId,
        CreateShoppingItemRequest request,
        HttpContext http,
        HttpHouseholdContext household,
        ShoppingItemService items,
        CancellationToken cancellationToken)
    {
        var caller = Caller(household);
        var result = await items.AddAsync(householdId, listId, request, caller.UserId, cancellationToken).ConfigureAwait(false);
        return result.ToHttpResult(http, item => item.Version);
    }

    private static async Task<IResult> ChangeItemAsync(
        Guid householdId,
        Guid itemId,
        ChangeShoppingItemRequest request,
        HttpContext http,
        HttpHouseholdContext household,
        ShoppingItemChangeService changes,
        CancellationToken cancellationToken)
    {
        var caller = Caller(household);
        var result = await changes
            .ChangeAsync(householdId, itemId, request, caller.UserId, caller.Role, cancellationToken)
            .ConfigureAwait(false);
        return result.ToHttpResult(http, change => change.Item.Version);
    }

    private static async Task<IResult> DeleteItemAsync(
        Guid householdId,
        Guid itemId,
        long baseVersion,
        HttpContext http,
        HttpHouseholdContext household,
        ShoppingItemChangeService changes,
        CancellationToken cancellationToken)
    {
        var caller = Caller(household);
        var result = await changes.DeleteAsync(householdId, itemId, baseVersion, caller.UserId, cancellationToken).ConfigureAwait(false);
        return result.ToHttpResult(http, change => change.Item.Version);
    }

    private static async Task<IResult> MoveItemAsync(
        Guid householdId,
        Guid itemId,
        MoveShoppingItemRequest request,
        HttpContext http,
        ShoppingItemService items,
        CancellationToken cancellationToken)
    {
        var result = await items.MoveAsync(householdId, itemId, request, cancellationToken).ConfigureAwait(false);
        return result.ToHttpResult(http, item => item.Version);
    }

    // The household policy has resolved the membership before any handler runs.
    private static HouseholdMembershipInfo Caller(HttpHouseholdContext household) =>
        household.Membership ?? throw new InvalidOperationException("Shopping endpoints require a resolved household membership.");
}
