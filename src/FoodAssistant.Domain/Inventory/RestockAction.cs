using FoodAssistant.Domain.Quantities;

namespace FoodAssistant.Domain.Inventory;

/// <summary>Outcome of <see cref="RestockDecision.Decide"/>.</summary>
/// <param name="Kind">What to do.</param>
/// <param name="Amount">Quantity to buy for <see cref="RestockKind.AddToList"/>, otherwise <see langword="null"/>.</param>
public sealed record RestockAction(RestockKind Kind, Quantity? Amount = null)
{
    public static RestockAction None { get; } = new(RestockKind.None);

    public static RestockAction Arm { get; } = new(RestockKind.Arm);

    public static RestockAction Disarm { get; } = new(RestockKind.Disarm);
}
