using FoodAssistant.Application.Checkout;
using FoodAssistant.Contracts.Checkout;
using FoodAssistant.Server.Api.Results;
using FoodAssistant.Server.Auth;

namespace FoodAssistant.Server.Checkout;

/// <summary>
/// <c>/api/v1/households/{householdId}/shopping/lists/{listId}/checkout</c>: suggestion and
/// completion of a shopping trip (docs/plan/features/m01-f-einkauf-abschliessen.md). Members only:
/// completing adds stock, which restricted members may not.
/// </summary>
internal static class CheckoutEndpoints
{
    public static RouteGroupBuilder MapCheckoutEndpoints(this RouteGroupBuilder household)
    {
        ArgumentNullException.ThrowIfNull(household);
        var checkout = household.MapGroup("/shopping/lists/{listId:guid}/checkout").WithTags("Checkout");
        checkout.MapGet("", ProposeAsync).RequireAuthorization(AuthPolicies.HouseholdMember);
        checkout.MapPost("", CompleteAsync).RequireAuthorization(AuthPolicies.HouseholdMember);
        return household;
    }

    private static async Task<IResult> ProposeAsync(
        Guid householdId,
        Guid listId,
        HttpContext http,
        CheckoutService checkout,
        CancellationToken cancellationToken) =>
        (await checkout.ProposeAsync(householdId, listId, cancellationToken).ConfigureAwait(false)).ToHttpResult(http);

    private static async Task<IResult> CompleteAsync(
        Guid householdId,
        Guid listId,
        CompleteCheckoutRequest request,
        HttpContext http,
        CheckoutService checkout,
        CancellationToken cancellationToken) =>
        (await checkout.CompleteAsync(householdId, listId, request, cancellationToken).ConfigureAwait(false)).ToHttpResult(http);
}
