namespace FoodAssistant.Application.Checkout;

/// <summary>One entry to remove from the list, with the batch it becomes (null when skipped).</summary>
/// <param name="ItemId">Shopping list entry.</param>
/// <param name="ExpectedVersion">Version the decision was based on; another one makes the line stale.</param>
/// <param name="Batch">New batch, or null for a skipped entry.</param>
public sealed record CheckoutCommitLine(Guid ItemId, long ExpectedVersion, CheckoutBatch? Batch);
