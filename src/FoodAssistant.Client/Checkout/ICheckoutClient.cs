using FoodAssistant.Client.Http;
using FoodAssistant.Contracts.Checkout;

namespace FoodAssistant.Client.Checkout;

/// <summary>Completing a shopping list: suggestion for the checked entries, then booking them into the stock.</summary>
public interface ICheckoutClient
{
    Task<ApiResult<CheckoutProposalDto>> GetProposalAsync(Guid householdId, Guid listId, CancellationToken cancellationToken);

    /// <summary>
    /// Books the lines. Send the same <paramref name="idempotencyKey"/> when repeating the same request
    /// after a failure without answer: the server then replays its answer instead of booking twice.
    /// </summary>
    Task<ApiResult<CheckoutResultDto>> CompleteAsync(
        Guid householdId, Guid listId, CompleteCheckoutRequest request, Guid idempotencyKey, CancellationToken cancellationToken);
}
