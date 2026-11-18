using FoodAssistant.Domain.Expiry;
using FoodAssistant.Domain.Inventory;
using FoodAssistant.Domain.Quantities;

namespace FoodAssistant.Domain.Checkout;

/// <summary>A bought batch entering the inventory (rules as for a new batch, docs/kb/vorrat-und-haltbarkeit.md).</summary>
public static class PurchasedBatch
{
    /// <summary>
    /// Longest shelf life remembered for the next purchase (10 years, like the shelf-life rules). A
    /// "never expires" date such as 31.12.9999 would otherwise be suggested again and overflow.
    /// </summary>
    public const int MaxRememberedDays = 3650;

    /// <summary>
    /// Creates the batch. In a freezer (F5): with a package date and not <paramref name="selfFrozen"/>
    /// it is bought frozen food (sealed, package date applies); otherwise it is frozen at home today
    /// (rule "frozen", the package date rests).
    /// </summary>
    /// <param name="id">Batch id.</param>
    /// <param name="quantity">Amount; must be greater than zero.</param>
    /// <param name="locationType">Storage type of the target location.</param>
    /// <param name="packageDate">Printed date, or null.</param>
    /// <param name="packageKind">Kind of the printed date; <see cref="ExpiryKind.None"/> exactly without date.</param>
    /// <param name="selfFrozen">Frozen at home although a package date is given.</param>
    /// <param name="frozenRule">Resolved rule for <see cref="StorageType.Frozen"/>.</param>
    /// <param name="today">Household today.</param>
    public static StockBatch Create(
        Guid id,
        Quantity quantity,
        StorageType locationType,
        DateOnly? packageDate,
        ExpiryKind packageKind,
        bool selfFrozen,
        ShelfLifeRule frozenRule,
        DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(frozenRule);
        if (IsFrozenAtHome(locationType, packageDate, selfFrozen))
        {
            var batch = new StockBatch(id, quantity, StorageType.Chilled, packageDate, packageKind);
            batch.Freeze(today, frozenRule);
            return batch;
        }

        return new StockBatch(id, quantity, locationType, packageDate, packageKind);
    }

    /// <summary>True if the batch goes into a freezer and its date comes from the rule "frozen".</summary>
    public static bool IsFrozenAtHome(StorageType locationType, DateOnly? packageDate, bool selfFrozen) =>
        locationType == StorageType.Frozen && (packageDate is null || selfFrozen);

    /// <summary>
    /// Shelf life to remember for the next purchase: only a printed date the person entered counts,
    /// never a rule-derived one (frozen at home), never one already past and none beyond
    /// <see cref="MaxRememberedDays"/>.
    /// </summary>
    /// <returns>Days from <paramref name="today"/> to the printed date with its kind, or null.</returns>
    public static LastPurchase? ShelfLifeOf(StockBatch batch, DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(batch);

        // A sealed batch with a printed date carries exactly that date; a frozen one rests it.
        if (batch.State != BatchState.Sealed || batch.PackageExpiresOn is not { } date)
        {
            return null;
        }

        var days = date.DayNumber - today.DayNumber;
        return days is >= 0 and <= MaxRememberedDays ? new LastPurchase(days, batch.PackageExpiryKind) : null;
    }
}
