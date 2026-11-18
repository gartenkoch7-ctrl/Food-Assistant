using FoodAssistant.Domain.Expiry;

namespace FoodAssistant.Domain.Inventory;

/// <summary>
/// Pure date arithmetic of the state table in docs/kb/vorrat-und-haltbarkeit.md, used by
/// <see cref="StockBatch"/>. Every rule-derived date is <see cref="ExpiryKind.UseBy"/> (see the
/// remarks on <see cref="StockBatch"/>).
/// </summary>
internal static class ShelfLifeDates
{
    /// <summary>Date <paramref name="days"/> after <paramref name="from"/>.</summary>
    /// <returns>No date (<see cref="ExpiryKind.None"/>) when the rule has no value: the batch is "without date" (KB).</returns>
    public static (DateOnly? Date, ExpiryKind Kind) FromRule(DateOnly from, int? days) =>
        days is { } value ? (from.AddDays(value), ExpiryKind.UseBy) : (null, ExpiryKind.None);

    /// <summary>
    /// Effective date after opening: min(package date, today + "after opening"). Without the rule
    /// value the package date and kind stay.
    /// </summary>
    public static (DateOnly? Date, ExpiryKind Kind) AfterOpening(
        DateOnly today,
        int? days,
        DateOnly? packageDate,
        ExpiryKind packageKind)
    {
        if (days is not { } value)
        {
            return (packageDate, packageKind);
        }

        var ruleDate = today.AddDays(value);

        // On a tie the rule wins so the stricter kind applies.
        return packageDate is not { } date || ruleDate <= date
            ? (ruleDate, ExpiryKind.UseBy)
            : (packageDate, packageKind);
    }
}
