using FoodAssistant.Domain.Inventory;

namespace FoodAssistant.Domain.Catalog;

/// <summary>
/// The fixed list of product categories (M1 decision F1). Keys are stable identifiers for storage
/// and the API; display names are translated in the UI. A category selects the shelf-life rules
/// and the default storage type of new products.
/// </summary>
public static class ProductCategories
{
    public const string Dairy = "dairy";
    public const string Cheese = "cheese";
    public const string Eggs = "eggs";
    public const string Meat = "meat";
    public const string Poultry = "poultry";
    public const string Fish = "fish";
    public const string Sausage = "sausage";
    public const string Fruit = "fruit";
    public const string Vegetables = "vegetables";
    public const string Herbs = "herbs";
    public const string Bread = "bread";
    public const string Grains = "grains";
    public const string Legumes = "legumes";
    public const string Canned = "canned";
    public const string Frozen = "frozen";
    public const string ReadyMeals = "ready_meals";
    public const string Sauces = "sauces";
    public const string Spices = "spices";
    public const string Oils = "oils";
    public const string Baking = "baking";
    public const string Sweets = "sweets";
    public const string Beverages = "beverages";
    public const string PlantBased = "plant_based";
    public const string Other = "other";

    private static readonly Dictionary<string, StorageType> DefaultStorageTypes = new(StringComparer.Ordinal)
    {
        [Dairy] = StorageType.Chilled,
        [Cheese] = StorageType.Chilled,
        [Eggs] = StorageType.Chilled,
        [Meat] = StorageType.Chilled,
        [Poultry] = StorageType.Chilled,
        [Fish] = StorageType.Chilled,
        [Sausage] = StorageType.Chilled,
        [Fruit] = StorageType.Ambient,
        [Vegetables] = StorageType.Chilled,
        [Herbs] = StorageType.Chilled,
        [Bread] = StorageType.Ambient,
        [Grains] = StorageType.Ambient,
        [Legumes] = StorageType.Ambient,
        [Canned] = StorageType.Ambient,
        [Frozen] = StorageType.Frozen,
        [ReadyMeals] = StorageType.Chilled,
        [Sauces] = StorageType.Ambient,
        [Spices] = StorageType.Ambient,
        [Oils] = StorageType.Ambient,
        [Baking] = StorageType.Ambient,
        [Sweets] = StorageType.Ambient,
        [Beverages] = StorageType.Ambient,
        [PlantBased] = StorageType.Chilled,
        [Other] = StorageType.Ambient,
    };

    /// <summary>All category keys in display order.</summary>
    public static IReadOnlyList<string> All { get; } =
    [
        Dairy, Cheese, Eggs, Meat, Poultry, Fish, Sausage, Fruit, Vegetables, Herbs, Bread, Grains,
        Legumes, Canned, Frozen, ReadyMeals, Sauces, Spices, Oils, Baking, Sweets, Beverages, PlantBased, Other,
    ];

    /// <summary>True if <paramref name="key"/> is one of <see cref="All"/> (case-sensitive).</summary>
    public static bool IsKnown(string? key) => key is not null && DefaultStorageTypes.ContainsKey(key);

    /// <summary>Storage type suggested for new products of the category; unknown keys → <see cref="StorageType.Ambient"/>.</summary>
    public static StorageType DefaultStorageType(string key) =>
        key is not null && DefaultStorageTypes.TryGetValue(key, out var type) ? type : StorageType.Ambient;
}
