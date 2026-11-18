using FoodAssistant.Application.Inventory;
using FoodAssistant.Server.Api.Concurrency;
using FoodAssistant.Server.Api.Pagination;
using FoodAssistant.Server.Api.Results;
using FoodAssistant.Server.Auth;
using FoodAssistant.Contracts.Inventory;

namespace FoodAssistant.Server.Inventory;

/// <summary>
/// <c>/api/v1/households/{householdId}/inventory</c>: overview, batches, consumption and undo
/// (docs/plan/features/m01-b2-chargen-verbrauch.md). Reading for every role, changes for members.
/// </summary>
internal static class InventoryEndpoints
{
    public static RouteGroupBuilder MapInventoryEndpoints(this RouteGroupBuilder household)
    {
        ArgumentNullException.ThrowIfNull(household);
        var inventory = household.MapGroup("/inventory").WithTags("Inventory");

        inventory.MapGet("", ListAsync).RequireAuthorization(AuthPolicies.HouseholdAny);
        inventory.MapGet("/products/{productId:guid}", GetProductAsync).RequireAuthorization(AuthPolicies.HouseholdAny);
        inventory.MapGet("/batches/{batchId:guid}", GetBatchAsync).RequireAuthorization(AuthPolicies.HouseholdAny);

        inventory.MapPost("/batches", AddAsync).RequireAuthorization(AuthPolicies.HouseholdMember);
        inventory.MapPut("/batches/{batchId:guid}", CorrectAsync).RequireAuthorization(AuthPolicies.HouseholdMember);
        inventory.MapPost("/batches/{batchId:guid}/move", MoveAsync).RequireAuthorization(AuthPolicies.HouseholdMember);
        inventory.MapPost("/batches/{batchId:guid}/open", OpenAsync).RequireAuthorization(AuthPolicies.HouseholdMember);
        inventory.MapPost("/batches/{batchId:guid}/consume", ConsumeBatchAsync).RequireAuthorization(AuthPolicies.HouseholdMember);
        inventory.MapPost("/leftovers", CreateLeftoverAsync).RequireAuthorization(AuthPolicies.HouseholdMember);
        inventory.MapPost("/products/{productId:guid}/consume", ConsumeProductAsync).RequireAuthorization(AuthPolicies.HouseholdMember);
        inventory.MapPost("/activities/{activityId:guid}/undo", UndoAsync).RequireAuthorization(AuthPolicies.HouseholdMember);
        return household;
    }

    private static async Task<IResult> ListAsync(
        Guid householdId,
        string? status,
        Guid? locationId,
        string? query,
        string? cursor,
        int? limit,
        HttpContext http,
        InventoryQueryService inventory,
        CancellationToken cancellationToken)
    {
        InventoryCursor? after = null;
        if (cursor is not null && !CursorPagination.TryDecode(cursor, out after))
        {
            return CursorPagination.InvalidCursor();
        }

        var take = CursorPagination.NormalizeLimit(limit);
        var result = await inventory
            .ListAsync(householdId, new InventoryQuery(status, locationId, query, after, take + 1), cancellationToken)
            .ConfigureAwait(false);
        return result.Value is { } items && result.Succeeded
            ? TypedResults.Ok(CursorPagination.ToPage(items, take, InventoryQueryService.PositionOf))
            : result.ToHttpResult(http);
    }

    private static async Task<IResult> GetProductAsync(
        Guid householdId,
        Guid productId,
        HttpContext http,
        InventoryQueryService inventory,
        CancellationToken cancellationToken) =>
        (await inventory.GetProductAsync(householdId, productId, cancellationToken).ConfigureAwait(false)).ToHttpResult(http);

    private static async Task<IResult> GetBatchAsync(
        Guid householdId,
        Guid batchId,
        HttpContext http,
        InventoryQueryService inventory,
        CancellationToken cancellationToken) =>
        (await inventory.GetBatchAsync(householdId, batchId, cancellationToken).ConfigureAwait(false))
            .ToHttpResult(http, batch => batch.Version);

    private static async Task<IResult> AddAsync(
        Guid householdId,
        AddBatchRequest request,
        HttpContext http,
        BatchAddService batches,
        CancellationToken cancellationToken) =>
        (await batches.AddAsync(householdId, request, cancellationToken).ConfigureAwait(false))
            .ToHttpResult(http, locationOf: BatchLocation);

    private static async Task<IResult> CorrectAsync(
        Guid householdId,
        Guid batchId,
        CorrectBatchRequest request,
        HttpContext http,
        BatchService batches,
        CancellationToken cancellationToken)
    {
        if (!IfMatch.TryRead(http, required: true, out var expected, out var problem))
        {
            return problem!;
        }

        return (await batches.CorrectAsync(householdId, batchId, expected, request, cancellationToken).ConfigureAwait(false))
            .ToHttpResult(http);
    }

    private static async Task<IResult> MoveAsync(
        Guid householdId,
        Guid batchId,
        MoveBatchRequest request,
        HttpContext http,
        BatchService batches,
        CancellationToken cancellationToken)
    {
        if (!IfMatch.TryRead(http, required: true, out var expected, out var problem))
        {
            return problem!;
        }

        return (await batches.MoveAsync(householdId, batchId, expected, request, cancellationToken).ConfigureAwait(false))
            .ToHttpResult(http);
    }

    private static async Task<IResult> OpenAsync(
        Guid householdId,
        Guid batchId,
        HttpContext http,
        BatchService batches,
        CancellationToken cancellationToken)
    {
        if (!IfMatch.TryRead(http, required: true, out var expected, out var problem))
        {
            return problem!;
        }

        return (await batches.OpenAsync(householdId, batchId, expected, cancellationToken).ConfigureAwait(false))
            .ToHttpResult(http);
    }

    // If-Match is optional: "used up" from a list must work without a fresh ETag; with one it is checked.
    private static async Task<IResult> ConsumeBatchAsync(
        Guid householdId,
        Guid batchId,
        ConsumeBatchRequest request,
        HttpContext http,
        ConsumptionService consumption,
        CancellationToken cancellationToken)
    {
        if (!IfMatch.TryRead(http, required: false, out var expected, out var problem))
        {
            return problem!;
        }

        return (await consumption.ConsumeBatchAsync(householdId, batchId, expected, request, cancellationToken).ConfigureAwait(false))
            .ToHttpResult(http);
    }

    private static async Task<IResult> CreateLeftoverAsync(
        Guid householdId,
        CreateLeftoverRequest request,
        HttpContext http,
        BatchAddService batches,
        CancellationToken cancellationToken) =>
        (await batches.CreateLeftoverAsync(householdId, request, cancellationToken).ConfigureAwait(false))
            .ToHttpResult(http, locationOf: BatchLocation);

    private static async Task<IResult> ConsumeProductAsync(
        Guid householdId,
        Guid productId,
        ConsumeProductRequest request,
        HttpContext http,
        ConsumptionService consumption,
        CancellationToken cancellationToken) =>
        (await consumption.ConsumeProductAsync(householdId, productId, request, cancellationToken).ConfigureAwait(false))
            .ToHttpResult(http);

    private static async Task<IResult> UndoAsync(
        Guid householdId,
        Guid activityId,
        HttpContext http,
        UndoService undo,
        CancellationToken cancellationToken) =>
        (await undo.UndoAsync(householdId, activityId, cancellationToken).ConfigureAwait(false)).ToHttpResult(http);

    // Relative to the request (…/inventory/batches or …/inventory/leftovers): the new batch's own URL.
    private static string BatchLocation(InventoryActionResponse response) => $"batches/{response.Batches[0].Id}";
}
