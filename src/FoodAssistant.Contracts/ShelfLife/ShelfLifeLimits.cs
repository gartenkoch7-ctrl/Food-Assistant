namespace FoodAssistant.Contracts.ShelfLife;

/// <summary>Value range of shelf-life days, shared by server validation, shipped table and UI input.</summary>
public static class ShelfLifeLimits
{
    /// <summary>Largest accepted value: ten years is far beyond any food, so more is a typing error.</summary>
    public const int MaxDays = 3650;
}
