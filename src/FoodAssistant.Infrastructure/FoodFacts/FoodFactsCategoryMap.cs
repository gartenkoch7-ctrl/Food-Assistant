using FoodAssistant.Domain.Catalog;

namespace FoodAssistant.Infrastructure.FoodFacts;

/// <summary>
/// Open Food Facts category tags → keys of <see cref="ProductCategories"/>. OFF products carry a
/// whole hierarchy of tags (a cheese is also <c>en:dairies</c>), so the table is ordered
/// specific before general and the first entry with a matching tag wins.
/// </summary>
internal static class FoodFactsCategoryMap
{
    private static readonly (string Tag, string Category)[] Table =
    [
        // Substitutes and storage forms decide how a product is kept, so they beat the base food.
        ("en:milk-substitutes", ProductCategories.PlantBased),
        ("en:frozen-foods", ProductCategories.Frozen),
        ("en:canned-foods", ProductCategories.Canned),
        ("en:cheeses", ProductCategories.Cheese),
        ("en:eggs", ProductCategories.Eggs),
        ("en:poultries", ProductCategories.Poultry),
        ("en:sausages", ProductCategories.Sausage),
        ("en:hams", ProductCategories.Sausage),
        ("en:meats", ProductCategories.Meat),
        ("en:fishes", ProductCategories.Fish),
        ("en:seafood", ProductCategories.Fish),
        ("en:yogurts", ProductCategories.Dairy),
        ("en:milks", ProductCategories.Dairy),
        ("en:dairies", ProductCategories.Dairy),
        ("en:meals", ProductCategories.ReadyMeals),
        ("en:breads", ProductCategories.Bread),
        ("en:pastries", ProductCategories.Bread),
        ("en:pastas", ProductCategories.Grains),
        ("en:rices", ProductCategories.Grains),
        ("en:cereals-and-their-products", ProductCategories.Grains),
        ("en:legumes", ProductCategories.Legumes),
        ("en:fruits", ProductCategories.Fruit),
        ("en:vegetables", ProductCategories.Vegetables),
        ("en:herbs", ProductCategories.Herbs),
        ("en:sauces", ProductCategories.Sauces),
        ("en:condiments", ProductCategories.Sauces),
        ("en:spices", ProductCategories.Spices),
        ("en:fats", ProductCategories.Oils),
        ("en:vegetable-oils", ProductCategories.Oils),
        ("en:baking-decorations", ProductCategories.Baking),
        ("en:flours", ProductCategories.Baking),
        ("en:sugars", ProductCategories.Baking),
        ("en:snacks", ProductCategories.Sweets),
        ("en:sweet-snacks", ProductCategories.Sweets),
        ("en:chocolates", ProductCategories.Sweets),
        ("en:beverages", ProductCategories.Beverages),

        // Very broad: nearly every plant food carries it, so only a last resort.
        ("en:plant-based-foods-and-beverages", ProductCategories.PlantBased),
    ];

    /// <summary>The category key of the first matching tag, or null.</summary>
    public static string? CategoryOf(IReadOnlyList<string> tags)
    {
        ArgumentNullException.ThrowIfNull(tags);
        foreach (var (tag, category) in Table)
        {
            if (tags.Contains(tag, StringComparer.Ordinal))
            {
                return category;
            }
        }

        return null;
    }
}
