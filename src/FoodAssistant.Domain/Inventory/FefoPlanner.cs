using FoodAssistant.Domain.Quantities;

namespace FoodAssistant.Domain.Inventory;

/// <summary>
/// Distributes a consumption over batches "first expired, first out" (docs/kb/vorrat-und-haltbarkeit.md):
/// earliest effective expiry date first, batches without a date last.
/// </summary>
public static class FefoPlanner
{
    /// <summary>Plans which batches to consume. Does not change the batches.</summary>
    /// <remarks>
    /// Batches with the same date keep their input order. Empty batches and batches whose unit does
    /// not convert into the requested unit are skipped; they cannot cover the amount.
    /// </remarks>
    /// <param name="batches">Batches of one product.</param>
    /// <param name="amount">Amount to consume; must be greater than zero.</param>
    public static FefoPlan Plan(IEnumerable<StockBatch> batches, Quantity amount)
    {
        ArgumentNullException.ThrowIfNull(batches);
        if (amount.Unit is null || amount.IsZero)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "The amount must be greater than zero.");
        }

        var ordered = batches
            .Where(batch => !batch.IsEmpty && batch.Quantity.CanConvertTo(amount.Unit))
            .OrderBy(batch => batch.ExpiresOn is null)
            .ThenBy(batch => batch.ExpiresOn);

        // Work in base units (g, ml, piece): sums and differences stay exact even when converting
        // between e.g. tablespoons and millilitres would produce repeating decimals.
        var allocations = new List<FefoAllocation>();
        var remainingBase = amount.ToBase();
        foreach (var batch in ordered)
        {
            if (remainingBase == 0m)
            {
                break;
            }

            var availableBase = batch.Quantity.ToBase();
            if (availableBase <= remainingBase)
            {
                allocations.Add(new FefoAllocation(batch.Id, batch.Quantity));
                remainingBase -= availableBase;
            }
            else
            {
                allocations.Add(new FefoAllocation(batch.Id, Quantity.FromBase(remainingBase, batch.Quantity.Unit)));
                remainingBase = 0m;
            }
        }

        return new FefoPlan(allocations, Quantity.FromBase(remainingBase, amount.Unit));
    }
}
