using FoodAssistant.Contracts.ShelfLife;

namespace FoodAssistant.UI.ShelfLife;

/// <summary>Input range of the shelf-life dialog; the same as the server's validation (<see cref="ShelfLifeLimits"/>).</summary>
public static class ShelfLifeDaysDialogLimits
{
    /// <summary>Empty is valid (no own value); otherwise 0–<see cref="ShelfLifeLimits.MaxDays"/>.</summary>
    public static bool IsValid(int? days) => days is null or (>= 0 and <= ShelfLifeLimits.MaxDays);
}
