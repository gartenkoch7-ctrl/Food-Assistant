using FoodAssistant.Domain.Quantities;

namespace FoodAssistant.Domain.Inventory;

/// <summary>
/// Minimum-stock rule (docs/kb/einkauf.md): a product goes on the list <b>once</b> when it falls
/// below its minimum; it comes back only after the stock was sufficient again in between.
/// </summary>
public static class RestockDecision
{
    /// <param name="minimum">Minimum stock of the product, or <see langword="null"/> if none is set.</param>
    /// <param name="total">Current stock; ignored without a minimum.</param>
    /// <param name="triggered">A minimum-stock restock was already started and not yet reset.</param>
    /// <param name="onOpenList">The product has an unchecked entry on some shopping list.</param>
    public static RestockAction Decide(Quantity? minimum, Quantity total, bool triggered, bool onOpenList)
    {
        if (minimum is not { } min)
        {
            return triggered ? RestockAction.Disarm : RestockAction.None;
        }

        // Stock in a unit that does not convert counts as nothing: at worst one extra entry, never a missing one.
        var stock = total.CanConvertTo(min.Unit) ? total : Quantity.Zero(min.Unit);
        if (stock >= min)
        {
            return triggered ? RestockAction.Disarm : RestockAction.None;
        }

        if (triggered)
        {
            return RestockAction.None;
        }

        return onOpenList ? RestockAction.Arm : new RestockAction(RestockKind.AddToList, AmountToBuy(min, stock));
    }

    // Whole packages cannot be bought in parts; measured amounts are exact, except that unit
    // conversion may leave more than the three decimals the shopping list stores: round up, so the
    // household never buys too little.
    private static Quantity AmountToBuy(Quantity minimum, Quantity stock)
    {
        var missing = minimum.Subtract(stock);
        return IsCountable(minimum.Unit)
            ? new Quantity(Math.Ceiling(missing.Value), minimum.Unit)
            : new Quantity(Math.Ceiling(missing.Value * 1000m) / 1000m, minimum.Unit);
    }

    private static bool IsCountable(Unit unit) =>
        ReferenceEquals(unit, Unit.Piece) || ReferenceEquals(unit, Unit.Package) || ReferenceEquals(unit, Unit.Can);
}
