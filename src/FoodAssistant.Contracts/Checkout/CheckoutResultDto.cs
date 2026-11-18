namespace FoodAssistant.Contracts.Checkout;

/// <summary>Result of completing a shopping trip.</summary>
/// <param name="BatchesCreated">Number of new batches.</param>
/// <param name="ItemsRemoved">Entries removed from the list (booked or skipped).</param>
/// <param name="BatchIds">The new batches.</param>
/// <param name="StaleItemIds">
/// Entries not processed because they changed meanwhile (no longer checked, deleted, moved or a
/// different version) or their product was archived; they stay as they are.
/// </param>
public sealed record CheckoutResultDto(
    int BatchesCreated,
    int ItemsRemoved,
    IReadOnlyList<Guid> BatchIds,
    IReadOnlyList<Guid> StaleItemIds);
