using FoodAssistant.Application.Households;
using FoodAssistant.Server.Catalog;
using FoodAssistant.Server.ShelfLife;
using FoodAssistant.Server.Inventory;
using FoodAssistant.Server.FoodFacts;
using FoodAssistant.Server.Shopping;
using FoodAssistant.Server.Stores;
using FoodAssistant.Server.Checkout;
using FoodAssistant.Server.Restock;
using FoodAssistant.Server.Notifications;
using FoodAssistant.Server.Statistics;
using FoodAssistant.Server.Labels;
using FoodAssistant.Server.Import;
using FoodAssistant.Server.Export;
using FoodAssistant.Server.Activities;
using FoodAssistant.Server.Sync;
using FoodAssistant.Contracts.Households;
using FoodAssistant.Server.Auth;

namespace FoodAssistant.Server.Households;

/// <summary>
/// <c>/api/v1/households</c>. Everything below <c>/{householdId}</c> requires a household policy;
/// it resolves the membership and activates the household query filter for the request.
/// </summary>
internal static class HouseholdEndpoints
{
    public static RouteGroupBuilder MapHouseholdEndpoints(this RouteGroupBuilder api)
    {
        var households = api.MapGroup("/households")
            .WithTags("Households")
            .AddEndpointFilter(new CookieAntiforgeryFilter(always: false));

        households.MapPost("", CreateAsync).RequireAuthorization(AuthPolicies.ServerAdmin);

        // Baseline for everything below: at least some membership. Endpoints add stricter policies;
        // one forgotten there still cannot reach a foreign household.
        var household = households
            .MapGroup($"/{{{AuthPolicies.HouseholdRouteValue}:guid}}")
            .RequireAuthorization(AuthPolicies.HouseholdAny);
        household.MapInvitationEndpoints();
        household.MapMemberEndpoints();
        household.MapCatalogEndpoints();
        household.MapShelfLifeEndpoints();
        household.MapInventoryEndpoints();
        household.MapFoodFactsEndpoints();
        household.MapShoppingEndpoints();
        household.MapStoresEndpoints();
        household.MapCheckoutEndpoints();
        household.MapRestockEndpoints();
        household.MapNotificationsEndpoints();
        household.MapStatisticsEndpoints();
        household.MapLabelsEndpoints();
        household.MapImportEndpoints();
        household.MapExportEndpoints();
        household.MapActivityEndpoints();
        household.MapSyncEndpoints();
        household.MapHouseholdSettingsEndpoints();
        households.MapHouseholdImportEndpoints();
        return api;
    }

    private static async Task<IResult> CreateAsync(
        CreateHouseholdRequest request,
        HttpContext httpContext,
        HouseholdService households,
        CancellationToken cancellationToken)
    {
        var result = await households
            .CreateAsync(request.Name, request.TimeZone, httpContext.User.GetRequiredUserId(), cancellationToken)
            .ConfigureAwait(false);
        return result.Household is { } household
            ? TypedResults.Created((string?)null, household)
            : ProblemResults.Validation(result.Errors, HouseholdErrorCodes.ValidationFailed);
    }
}
