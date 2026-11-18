namespace FoodAssistant.Application.Checkout;

/// <summary>Lines ready to commit, entries already stale when read, or field errors.</summary>
/// <param name="Lines">Lines to commit.</param>
/// <param name="StaleItemIds">Entries no longer checked on the list or with another version.</param>
/// <param name="Errors">Field errors (unknown product or location); nothing is committed then.</param>
public sealed record CheckoutPlan(
    IReadOnlyList<CheckoutCommitLine> Lines,
    IReadOnlyList<Guid> StaleItemIds,
    IReadOnlyDictionary<string, string[]> Errors);
