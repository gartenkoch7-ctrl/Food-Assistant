using FoodAssistant.Application.ShelfLife;
using FoodAssistant.Contracts.ShelfLife;
using FoodAssistant.Server.Api.Results;
using FoodAssistant.Server.Auth;
using Microsoft.AspNetCore.Http.HttpResults;

namespace FoodAssistant.Server.ShelfLife;

/// <summary>
/// Shelf-life rules of a household: shipped defaults with sources, household overrides per category
/// and per product. Reading is open to every member, writing needs at least the member role.
/// </summary>
/// <remarks>
/// Overrides are written without <c>If-Match</c>: a PUT carries the complete values of one row and
/// the row has no other state, so "last write wins" is the expected behavior (see
/// <see cref="ShelfLifeAdminService"/>).
/// </remarks>
internal static class ShelfLifeEndpoints
{
    public static RouteGroupBuilder MapShelfLifeEndpoints(this RouteGroupBuilder household)
    {
        ArgumentNullException.ThrowIfNull(household);
        var group = household.MapGroup("").WithTags("ShelfLife");

        group.MapGet("/shelf-life/rules", ListRulesAsync).RequireAuthorization(AuthPolicies.HouseholdAny);
        group.MapGet("/shelf-life/sources", ListSources).RequireAuthorization(AuthPolicies.HouseholdAny);
        group.MapPut("/shelf-life/overrides/categories/{categoryKey}/{storageType}", SetCategoryAsync)
            .RequireAuthorization(AuthPolicies.HouseholdMember);
        group.MapDelete("/shelf-life/overrides/categories/{categoryKey}/{storageType}", ResetCategoryAsync)
            .RequireAuthorization(AuthPolicies.HouseholdMember);

        group.MapGet("/products/{productId:guid}/shelf-life", GetProductAsync).RequireAuthorization(AuthPolicies.HouseholdAny);
        group.MapPut("/products/{productId:guid}/shelf-life/{storageType}", SetProductAsync)
            .RequireAuthorization(AuthPolicies.HouseholdMember);
        group.MapDelete("/products/{productId:guid}/shelf-life/{storageType}", ResetProductAsync)
            .RequireAuthorization(AuthPolicies.HouseholdMember);
        return household;
    }

    private static async Task<Ok<IReadOnlyList<ShelfLifeRuleRowDto>>> ListRulesAsync(
        Guid householdId,
        ShelfLifeAdminService rules,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await rules.ListRulesAsync(householdId, cancellationToken).ConfigureAwait(false));

    private static Ok<IReadOnlyList<ShelfLifeSourceDto>> ListSources(ShelfLifeAdminService rules) =>
        TypedResults.Ok(rules.ListSources());

    private static async Task<IResult> SetCategoryAsync(
        Guid householdId,
        string categoryKey,
        string storageType,
        ShelfLifeDaysDto days,
        HttpContext http,
        ShelfLifeAdminService rules,
        CancellationToken cancellationToken) =>
        (await rules.SetCategoryOverrideAsync(householdId, categoryKey, storageType, days, cancellationToken).ConfigureAwait(false))
            .ToHttpResult(http);

    private static async Task<IResult> ResetCategoryAsync(
        Guid householdId,
        string categoryKey,
        string storageType,
        HttpContext http,
        ShelfLifeAdminService rules,
        CancellationToken cancellationToken) =>
        (await rules.ResetCategoryOverrideAsync(householdId, categoryKey, storageType, cancellationToken).ConfigureAwait(false))
            .ToHttpResult(http);

    private static async Task<IResult> GetProductAsync(
        Guid householdId,
        Guid productId,
        HttpContext http,
        ProductShelfLifeService products,
        CancellationToken cancellationToken) =>
        (await products.GetAsync(householdId, productId, cancellationToken).ConfigureAwait(false)).ToHttpResult(http);

    private static async Task<IResult> SetProductAsync(
        Guid householdId,
        Guid productId,
        string storageType,
        ShelfLifeDaysDto days,
        HttpContext http,
        ProductShelfLifeService products,
        CancellationToken cancellationToken) =>
        (await products.SetOverrideAsync(householdId, productId, storageType, days, cancellationToken).ConfigureAwait(false))
            .ToHttpResult(http);

    private static async Task<IResult> ResetProductAsync(
        Guid householdId,
        Guid productId,
        string storageType,
        HttpContext http,
        ProductShelfLifeService products,
        CancellationToken cancellationToken) =>
        (await products.ResetOverrideAsync(householdId, productId, storageType, cancellationToken).ConfigureAwait(false))
            .ToHttpResult(http);
}
