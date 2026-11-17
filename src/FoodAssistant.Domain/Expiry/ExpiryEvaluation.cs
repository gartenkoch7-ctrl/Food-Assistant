namespace FoodAssistant.Domain.Expiry;

/// <summary>Result of <see cref="ExpiryEvaluator.Evaluate"/>.</summary>
/// <param name="Status">Status according to the knowledge base table.</param>
/// <param name="DaysUntilExpiry">
/// Days from today to the date: 0 = today, 1 = tomorrow, negative = expired that many days ago;
/// null without a date.
/// </param>
public readonly record struct ExpiryEvaluation(ExpiryStatus Status, int? DaysUntilExpiry)
{
    /// <summary>Days since the date passed (1 = since yesterday); null unless expired.</summary>
    public int? DaysSinceExpiry => Status == ExpiryStatus.Expired ? -DaysUntilExpiry : null;
}
