using FoodAssistant.Client.Http;
using FoodAssistant.Contracts.Checkout;

namespace FoodAssistant.Client.Checkout;

/// <summary>The checkout API of a household over <see cref="ApiHttpClient"/>.</summary>
public sealed class CheckoutClient(ApiHttpClient api) : ICheckoutClient
{
    public Task<ApiResult<CheckoutProposalDto>> GetProposalAsync(Guid householdId, Guid listId, CancellationToken cancellationToken) =>
        api.GetAsync<CheckoutProposalDto>(Path(householdId, listId), cancellationToken);

    public Task<ApiResult<CheckoutResultDto>> CompleteAsync(
        Guid householdId, Guid listId, CompleteCheckoutRequest request, Guid idempotencyKey, CancellationToken cancellationToken) =>
        api.SendAsync<CompleteCheckoutRequest, CheckoutResultDto>(
            HttpMethod.Post, Path(householdId, listId), request, null, idempotencyKey, cancellationToken);

    private static string Path(Guid householdId, Guid listId) =>
        ApiPaths.Household(householdId, $"shopping/lists/{listId:D}/checkout");
}
