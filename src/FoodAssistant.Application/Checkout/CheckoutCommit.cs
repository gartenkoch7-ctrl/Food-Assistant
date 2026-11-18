namespace FoodAssistant.Application.Checkout;

/// <summary>Everything completing a list writes, in one transaction.</summary>
/// <param name="ListId">The list.</param>
/// <param name="Now">Time of the change, cut to microseconds (undo compares <c>UpdatedAt</c>).</param>
/// <param name="Today">Household date the batches are added.</param>
/// <param name="Lines">Entries to process.</param>
public sealed record CheckoutCommit(Guid ListId, DateTimeOffset Now, DateOnly Today, IReadOnlyList<CheckoutCommitLine> Lines);
