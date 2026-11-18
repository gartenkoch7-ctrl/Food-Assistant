using FoodAssistant.Application.Restock;
using FoodAssistant.Contracts.Restock;
using FoodAssistant.Server.Api.Concurrency;
using FoodAssistant.Server.Api.Results;
using FoodAssistant.Server.Auth;

namespace FoodAssistant.Server.Restock;

/// <summary>
/// <c>/api/v1/households/{householdId}/shopping/recurring</c>: recurring shopping rules. Everyone
/// reads, members and owners write; <c>PUT</c> and <c>DELETE</c> need <c>If-Match</c>.
/// </summary>
internal static class RestockEndpoints
{
    public static RouteGroupBuilder MapRestockEndpoints(this RouteGroupBuilder household)
    {
        ArgumentNullException.ThrowIfNull(household);
        var recurring = household.MapGroup("/shopping/recurring").WithTags("Restock");

        recurring.MapGet("", ListAsync)
            .RequireAuthorization(AuthPolicies.HouseholdAny)
            .Produces<IReadOnlyList<RecurringRuleDto>>();
        recurring.MapPost("", CreateAsync)
            .RequireAuthorization(AuthPolicies.HouseholdMember)
            .Produces<RecurringRuleDto>(StatusCodes.Status201Created);
        recurring.MapPut("/{ruleId:guid}", UpdateAsync)
            .RequireAuthorization(AuthPolicies.HouseholdMember)
            .Produces<RecurringRuleDto>();
        recurring.MapDelete("/{ruleId:guid}", DeleteAsync)
            .RequireAuthorization(AuthPolicies.HouseholdMember)
            .Produces(StatusCodes.Status204NoContent);
        return household;
    }

    private static async Task<IResult> ListAsync(
        Guid householdId,
        RecurringRuleService rules,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await rules.ListAsync(householdId, cancellationToken).ConfigureAwait(false));

    private static async Task<IResult> CreateAsync(
        Guid householdId,
        RecurringRuleRequest request,
        HttpContext http,
        RecurringRuleService rules,
        CancellationToken cancellationToken) =>
        (await rules.CreateAsync(householdId, request, cancellationToken).ConfigureAwait(false))
            .ToHttpResult(http, rule => rule.Version);

    private static async Task<IResult> UpdateAsync(
        Guid householdId,
        Guid ruleId,
        RecurringRuleRequest request,
        HttpContext http,
        RecurringRuleService rules,
        CancellationToken cancellationToken)
    {
        if (!IfMatch.TryRead(http, required: true, out var expected, out var problem))
        {
            return problem!;
        }

        var result = await rules.UpdateAsync(householdId, ruleId, expected, request, cancellationToken).ConfigureAwait(false);
        return result.ToHttpResult(http, rule => rule.Version);
    }

    private static async Task<IResult> DeleteAsync(
        Guid householdId,
        Guid ruleId,
        HttpContext http,
        RecurringRuleService rules,
        CancellationToken cancellationToken)
    {
        if (!IfMatch.TryRead(http, required: true, out var expected, out var problem))
        {
            return problem!;
        }

        var result = await rules.DeleteAsync(householdId, ruleId, expected, cancellationToken).ConfigureAwait(false);
        return result.ToHttpResult(http, rule => rule.Version);
    }
}
