using FoodAssistant.Application.Households;
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
