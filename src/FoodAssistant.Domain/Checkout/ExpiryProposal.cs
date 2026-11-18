using FoodAssistant.Domain.Expiry;

namespace FoodAssistant.Domain.Checkout;

/// <summary>Expiry date suggested for a bought entry when completing a shopping trip.</summary>
/// <param name="ExpiresOn">Suggested date, or null for "without date" (the person enters one).</param>
/// <param name="Kind">Kind of the date; <see cref="ExpiryKind.None"/> exactly when there is no date.</param>
/// <param name="FromRule">
/// True if the date comes from the shelf-life rule "frozen" (frozen at home): the client then sends
/// no package date, and the server computes the same date again.
/// </param>
public sealed record ExpiryProposal(DateOnly? ExpiresOn, ExpiryKind Kind, bool FromRule)
{
    /// <summary>No suggestion: the person enters the date.</summary>
    public static ExpiryProposal None { get; } = new(null, ExpiryKind.None, false);
}
