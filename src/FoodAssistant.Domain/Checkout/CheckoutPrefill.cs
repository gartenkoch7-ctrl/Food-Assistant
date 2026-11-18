using FoodAssistant.Domain.Expiry;
using FoodAssistant.Domain.Inventory;

namespace FoodAssistant.Domain.Checkout;

/// <summary>
/// Suggestions for completing a shopping trip: storage location and expiry date of each bought
/// entry (docs/plan/features/m01-f-einkauf-abschliessen.md, docs/kb/einkauf.md).
/// </summary>
public static class CheckoutPrefill
{
    /// <summary>
    /// Location cascade: the product's default location, else the location of its last added batch,
    /// else the first location of the category's default storage type, else the first location.
    /// </summary>
    /// <param name="productDefault">Default location of the product, if any.</param>
    /// <param name="lastBatchLocation">Location of the product's most recently added batch, if any.</param>
    /// <param name="categoryType">Default storage type of the product's category; null for free text.</param>
    /// <param name="locations">All locations of the household in display order (<c>SortOrder</c>).</param>
    /// <returns>The suggested location, or null if the household has none.</returns>
    public static LocationOption? ProposeLocation(
        Guid? productDefault,
        Guid? lastBatchLocation,
        StorageType? categoryType,
        IReadOnlyList<LocationOption> locations)
    {
        ArgumentNullException.ThrowIfNull(locations);

        // A stored id may point to a location deleted meanwhile; only existing ones count.
        return Find(locations, productDefault)
            ?? Find(locations, lastBatchLocation)
            ?? (categoryType is { } type ? locations.FirstOrDefault(l => l.Type == type) : null)
            ?? (locations.Count > 0 ? locations[0] : null);
    }

    /// <summary>
    /// Expiry suggestion. A known last purchase suggests today plus its days with its kind. Into a
    /// freezer without one the batch counts as frozen at home (F5): rule "frozen" from today, as
    /// use-by date. Otherwise there is no date and the person enters one.
    /// </summary>
    /// <param name="targetType">Storage type of the suggested location; null if there is none.</param>
    /// <param name="lastPurchase">Shelf life of the last purchase of the product, if known.</param>
    /// <param name="frozenRule">Resolved rule for <see cref="StorageType.Frozen"/>; only read for a freezer.</param>
    /// <param name="today">Household today.</param>
    public static ExpiryProposal Propose(
        StorageType? targetType,
        LastPurchase? lastPurchase,
        ShelfLifeRule frozenRule,
        DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(frozenRule);
        if (lastPurchase is { } last && last.Kind != ExpiryKind.None)
        {
            return new ExpiryProposal(today.AddDays(last.Days), last.Kind, FromRule: false);
        }

        if (targetType == StorageType.Frozen)
        {
            // Without a rule value the batch stays without date, exactly like frozen at home (F5).
            return frozenRule.DaysFrozen is { } days
                ? new ExpiryProposal(today.AddDays(days), ExpiryKind.UseBy, FromRule: true)
                : ExpiryProposal.None;
        }

        return ExpiryProposal.None;
    }

    private static LocationOption? Find(IReadOnlyList<LocationOption> locations, Guid? id) =>
        id is { } wanted ? locations.FirstOrDefault(l => l.Id == wanted) : null;
}
