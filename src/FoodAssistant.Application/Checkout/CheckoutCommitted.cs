namespace FoodAssistant.Application.Checkout;

/// <summary>What <see cref="ICheckoutStore.CommitAsync"/> stored.</summary>
/// <param name="Applied">Lines that were written.</param>
/// <param name="StaleItemIds">Lines left out: the entry changed since loading or its product was archived.</param>
public sealed record CheckoutCommitted(IReadOnlyList<CheckoutCommitLine> Applied, IReadOnlyList<Guid> StaleItemIds);
