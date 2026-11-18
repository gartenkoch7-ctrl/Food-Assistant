using FoodAssistant.Application.Households;
using FoodAssistant.Contracts.Households;
using FoodAssistant.Server.Api.Concurrency;
using FoodAssistant.Server.Api.Results;
using FoodAssistant.Server.Auth;

namespace FoodAssistant.Server.Households;

/// <summary>
/// <c>GET/PUT /api/v1/households/{householdId}</c>: every member reads the settings (with ETag),
/// only owners change them, and only with <c>If-Match</c> (ADR-0004).
/// </summary>
internal static class HouseholdSettingsEndpoints
{
    public static RouteGroupBuilder MapHouseholdSettingsEndpoints(this RouteGroupBuilder household)
    {
        ArgumentNullException.ThrowIfNull(household);

        household.MapGet("", GetAsync).WithTags("Households");
        household.MapPut("", UpdateAsync).WithTags("Households").RequireAuthorization(AuthPolicies.HouseholdOwner);
        return household;
    }

    private static async Task<IResult> GetAsync(
        Guid householdId,
        HttpContext http,
        HouseholdSettingsService settings,
        CancellationToken cancellationToken) =>
        (await settings.GetAsync(householdId, cancellationToken).ConfigureAwait(false))
            .ToHttpResult(http, s => s.Version);

    private static async Task<IResult> UpdateAsync(
        Guid householdId,
        UpdateHouseholdRequest request,
        HttpContext http,
        HouseholdSettingsService settings,
        CancellationToken cancellationToken)
    {
        if (!IfMatch.TryRead(http, required: true, out var expectedVersion, out var problem))
        {
            return problem!;
        }

        return (await settings.UpdateAsync(householdId, request, expectedVersion, cancellationToken).ConfigureAwait(false))
            .ToHttpResult(http, s => s.Version);
    }
}
