using FoodAssistant.Application.Stores;
using FoodAssistant.Contracts.Stores;
using FoodAssistant.Server.Api.Concurrency;
using FoodAssistant.Server.Api.Results;
using FoodAssistant.Server.Auth;

namespace FoodAssistant.Server.Stores;

/// <summary>
/// <c>/api/v1/households/{householdId}/stores</c> and the section order of each store (learned from
/// check-offs, manual order first). <c>/shopping/sections</c> is the default order without a store.
/// </summary>
internal static class StoresEndpoints
{
    public static RouteGroupBuilder MapStoresEndpoints(this RouteGroupBuilder household)
    {
        ArgumentNullException.ThrowIfNull(household);
        household.MapGet("/shopping/sections", DefaultSectionsAsync)
            .WithTags("Stores")
            .RequireAuthorization(AuthPolicies.HouseholdAny)
            .Produces<SectionOrderDto>();

        var stores = household.MapGroup("/stores").WithTags("Stores");
        stores.MapGet("", ListAsync)
            .RequireAuthorization(AuthPolicies.HouseholdAny)
            .Produces<IReadOnlyList<StoreDto>>();
        stores.MapPost("", CreateAsync)
            .RequireAuthorization(AuthPolicies.HouseholdMember)
            .Produces<StoreDto>(StatusCodes.Status201Created);
        stores.MapPut("/{storeId:guid}", UpdateAsync)
            .RequireAuthorization(AuthPolicies.HouseholdMember)
            .Produces<StoreDto>();
        stores.MapDelete("/{storeId:guid}", DeleteAsync)
            .RequireAuthorization(AuthPolicies.HouseholdMember)
            .Produces(StatusCodes.Status204NoContent);

        stores.MapGet("/{storeId:guid}/section-order", GetSectionOrderAsync)
            .RequireAuthorization(AuthPolicies.HouseholdAny)
            .Produces<SectionOrderDto>();
        stores.MapPut("/{storeId:guid}/section-order", SetSectionOrderAsync)
            .RequireAuthorization(AuthPolicies.HouseholdMember)
            .Produces<SectionOrderDto>();
        stores.MapDelete("/{storeId:guid}/section-order", ResetSectionOrderAsync)
            .RequireAuthorization(AuthPolicies.HouseholdMember)
            .Produces<SectionOrderDto>();
        return household;
    }

    private static async Task<IResult> DefaultSectionsAsync(
        Guid householdId,
        HttpContext http,
        StoreService stores,
        CancellationToken cancellationToken) =>
        (await stores.GetSectionOrderAsync(householdId, storeId: null, cancellationToken).ConfigureAwait(false)).ToHttpResult(http);

    private static async Task<IResult> ListAsync(Guid householdId, StoreService stores, CancellationToken cancellationToken) =>
        TypedResults.Ok(await stores.ListAsync(householdId, cancellationToken).ConfigureAwait(false));

    private static async Task<IResult> CreateAsync(
        Guid householdId,
        StoreRequest request,
        HttpContext http,
        StoreService stores,
        CancellationToken cancellationToken) =>
        (await stores.CreateAsync(householdId, request, cancellationToken).ConfigureAwait(false))
            .ToHttpResult(http, store => store.Version);

    private static async Task<IResult> UpdateAsync(
        Guid householdId,
        Guid storeId,
        StoreRequest request,
        HttpContext http,
        StoreService stores,
        CancellationToken cancellationToken)
    {
        if (!IfMatch.TryRead(http, required: true, out var expected, out var problem))
        {
            return problem!;
        }

        var result = await stores.UpdateAsync(householdId, storeId, expected, request, cancellationToken).ConfigureAwait(false);
        return result.ToHttpResult(http, store => store.Version);
    }

    private static async Task<IResult> DeleteAsync(
        Guid householdId,
        Guid storeId,
        HttpContext http,
        StoreService stores,
        CancellationToken cancellationToken)
    {
        if (!IfMatch.TryRead(http, required: true, out var expected, out var problem))
        {
            return problem!;
        }

        var result = await stores.DeleteAsync(householdId, storeId, expected, cancellationToken).ConfigureAwait(false);
        return result.ToHttpResult(http, store => store.Version);
    }

    private static async Task<IResult> GetSectionOrderAsync(
        Guid householdId,
        Guid storeId,
        HttpContext http,
        StoreService stores,
        CancellationToken cancellationToken) =>
        (await stores.GetSectionOrderAsync(householdId, storeId, cancellationToken).ConfigureAwait(false)).ToHttpResult(http);

    private static async Task<IResult> SetSectionOrderAsync(
        Guid householdId,
        Guid storeId,
        SetSectionOrderRequest request,
        HttpContext http,
        StoreService stores,
        CancellationToken cancellationToken) =>
        (await stores.SetSectionOrderAsync(householdId, storeId, request, cancellationToken).ConfigureAwait(false)).ToHttpResult(http);

    private static async Task<IResult> ResetSectionOrderAsync(
        Guid householdId,
        Guid storeId,
        HttpContext http,
        StoreService stores,
        CancellationToken cancellationToken) =>
        (await stores.ResetSectionOrderAsync(householdId, storeId, cancellationToken).ConfigureAwait(false)).ToHttpResult(http);
}
