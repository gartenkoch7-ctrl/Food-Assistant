using FoodAssistant.Domain.Catalog;

namespace FoodAssistant.Domain.Shopping;

/// <summary>
/// The fixed list of store sections (M1 decision F3). Keys are stable identifiers for storage and
/// the API; display names are translated in the UI. Each store learns its own order of these
/// sections; <see cref="All"/> is the order before anything is learned.
/// </summary>
public static class StoreSections
{
    public const string Produce = "produce";
    public const string Bakery = "bakery";
    public const string DairyChilled = "dairy_chilled";
    public const string MeatFish = "meat_fish";
    public const string Frozen = "frozen";
    public const string Pantry = "pantry";
    public const string SpicesBaking = "spices_baking";
    public const string SweetsSnacks = "sweets_snacks";
    public const string Beverages = "beverages";
    public const string Household = "household";
    public const string Other = "other";

    private static readonly Dictionary<string, string> SectionByCategory = new(StringComparer.Ordinal)
    {
        [ProductCategories.Dairy] = DairyChilled,
        [ProductCategories.Cheese] = DairyChilled,
        [ProductCategories.Eggs] = DairyChilled,
        [ProductCategories.Meat] = MeatFish,
        [ProductCategories.Poultry] = MeatFish,
        [ProductCategories.Fish] = MeatFish,
        [ProductCategories.Sausage] = MeatFish,
        [ProductCategories.Fruit] = Produce,
        [ProductCategories.Vegetables] = Produce,
        [ProductCategories.Herbs] = Produce,
        [ProductCategories.Bread] = Bakery,
        [ProductCategories.Grains] = Pantry,
        [ProductCategories.Legumes] = Pantry,
        [ProductCategories.Canned] = Pantry,
        [ProductCategories.Frozen] = Frozen,
        [ProductCategories.ReadyMeals] = DairyChilled,
        [ProductCategories.Sauces] = Pantry,
        [ProductCategories.Spices] = SpicesBaking,
        [ProductCategories.Oils] = Pantry,
        [ProductCategories.Baking] = SpicesBaking,
        [ProductCategories.Sweets] = SweetsSnacks,
        [ProductCategories.Beverages] = Beverages,
        [ProductCategories.PlantBased] = DairyChilled,
        [ProductCategories.Other] = Other,
    };

    /// <summary>All section keys in the default order.</summary>
    public static IReadOnlyList<string> All { get; } =
    [
        Produce, Bakery, DairyChilled, MeatFish, Frozen, Pantry, SpicesBaking, SweetsSnacks, Beverages, Household, Other,
    ];

    /// <summary>True if <paramref name="key"/> is one of <see cref="All"/> (case-sensitive).</summary>
    public static bool IsKnown(string? key) => key is not null && All.Contains(key, StringComparer.Ordinal);

    /// <summary>Default section of a product category; unknown keys → <see cref="Other"/>.</summary>
    public static string ForCategory(string categoryKey) =>
        categoryKey is not null && SectionByCategory.TryGetValue(categoryKey, out var section) ? section : Other;
}
