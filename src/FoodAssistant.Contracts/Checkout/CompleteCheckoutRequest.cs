namespace FoodAssistant.Contracts.Checkout;

/// <summary>
/// Completes a shopping trip in one transaction. Send an <c>Idempotency-Key</c>: a retry then gets
/// the same answer instead of booking twice.
/// </summary>
public sealed record CompleteCheckoutRequest(IReadOnlyList<CheckoutLineRequest> Lines);
