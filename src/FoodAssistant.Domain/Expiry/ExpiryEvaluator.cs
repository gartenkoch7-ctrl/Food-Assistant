namespace FoodAssistant.Domain.Expiry;

/// <summary>
/// Classifies an expiry date exactly by the status table in docs/kb/vorrat-und-haltbarkeit.md.
/// </summary>
public static class ExpiryEvaluator
{
    /// <summary>Default lead time in days for <see cref="ExpiryStatus.Soon"/>.</summary>
    public const int DefaultLeadDays = 2;

    /// <summary>Evaluates <paramref name="expiresOn"/> against <paramref name="today"/>.</summary>
    /// <param name="expiresOn">Effective expiry date, or null if there is none.</param>
    /// <param name="today">Calendar day in the household's time zone; never read from a clock here.</param>
    /// <param name="leadDays">Lead time for "soon"; 0 means only today counts as soon.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="leadDays"/> is negative.</exception>
    public static ExpiryEvaluation Evaluate(DateOnly? expiresOn, DateOnly today, int leadDays = DefaultLeadDays)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(leadDays);
        if (expiresOn is not { } date)
        {
            return new ExpiryEvaluation(ExpiryStatus.NoDate, null);
        }

        var days = date.DayNumber - today.DayNumber;
        ExpiryStatus status;
        if (days < 0)
        {
            status = ExpiryStatus.Expired;
        }
        else if (days <= leadDays)
        {
            status = ExpiryStatus.Soon;
        }
        else
        {
            status = ExpiryStatus.Ok;
        }

        return new ExpiryEvaluation(status, days);
    }
}
