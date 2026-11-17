using FoodAssistant.Domain.Quantities;

namespace FoodAssistant.Domain.Inventory;

/// <summary>
/// Decides whether a product needs restocking: stock below the minimum stock (docs/kb/einkauf.md).
/// </summary>
public static class MinimumStockPolicy
{
    /// <summary>Sums the batches in <paramref name="unit"/>.</summary>
    /// <remarks>
    /// Batches whose unit does not convert are not counted. The KB does not decide this; leaving them
    /// out is conservative because it can only lead to an extra list entry, never to a missing one.
    /// </remarks>
    public static Quantity TotalStock(IEnumerable<StockBatch> batches, Unit unit)
    {
        ArgumentNullException.ThrowIfNull(batches);
        ArgumentNullException.ThrowIfNull(unit);
        return batches
            .Select(batch => batch.Quantity)
            .Where(quantity => quantity.CanConvertTo(unit))
            .Aggregate(Quantity.Zero(unit), (total, quantity) => total.Add(quantity));
    }

    /// <summary>True if the stock is below <paramref name="minimum"/>; equal to the minimum is enough.</summary>
    public static bool NeedsRestock(IEnumerable<StockBatch> batches, Quantity minimum)
    {
        ArgumentNullException.ThrowIfNull(minimum.Unit);
        return TotalStock(batches, minimum.Unit) < minimum;
    }
}
