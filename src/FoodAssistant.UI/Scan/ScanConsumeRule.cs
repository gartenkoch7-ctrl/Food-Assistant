using FoodAssistant.Contracts.Common;
using FoodAssistant.Contracts.Inventory;

namespace FoodAssistant.UI.Scan;

/// <summary>
/// What scanning "used" takes out of stock (decision F6): one piece, package or can from a counted
/// product, the whole earliest batch for mass and volume (a scale reading is not known at the shelf).
/// Pure logic; the person can correct the amount afterwards.
/// </summary>
public static class ScanConsumeRule
{
    /// <summary>The batch that is used first: earliest effective date, batches without a date last.</summary>
    public static BatchDto? Earliest(IReadOnlyList<BatchDto> batches)
    {
        ArgumentNullException.ThrowIfNull(batches);
        return batches
            .OrderBy(b => b.ExpiresOn ?? DateOnly.MaxValue)
            .FirstOrDefault();
    }

    public static ConsumeProductRequest For(BatchDto earliest)
    {
        ArgumentNullException.ThrowIfNull(earliest);
        var counted = earliest.Quantity.Unit is UnitCodes.Piece or UnitCodes.Package or UnitCodes.Can;

        // Exactly one (or less) left: taking "1" would be the whole batch anyway, so say used up.
        return counted && earliest.Quantity.Value > 1m
            ? new ConsumeProductRequest(ConsumptionKindDto.Partial, new QuantityDto(1m, earliest.Quantity.Unit), null)
            : new ConsumeProductRequest(ConsumptionKindDto.UsedUp, null, null);
    }
}
