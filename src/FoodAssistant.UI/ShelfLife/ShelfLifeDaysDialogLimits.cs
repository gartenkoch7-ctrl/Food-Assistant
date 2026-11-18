namespace FoodAssistant.UI.ShelfLife;

/// <summary>Input range of the shelf-life dialog; the same as the server's validation (0–3650 days).</summary>
public static class ShelfLifeDaysDialogLimits
{
    public const int MaxDays = 3650;

    /// <summary>Empty is valid (no own value); otherwise 0–<see cref="MaxDays"/>.</summary>
    public static bool IsValid(int? days) => days is null or (>= 0 and <= MaxDays);
}
