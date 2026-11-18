using FoodAssistant.Domain.Catalog;
using FoodAssistant.Infrastructure.FoodFacts;

namespace FoodAssistant.Infrastructure.Tests.FoodFacts;

public sealed class FoodFactsCategoryMapTests
{
    [Theory]
    [InlineData("en:cheeses", "cheese")]
    [InlineData("en:yogurts", "dairy")]
    [InlineData("en:eggs", "eggs")]
    [InlineData("en:poultries", "poultry")]
    [InlineData("en:hams", "sausage")]
    [InlineData("en:seafood", "fish")]
    [InlineData("en:pastas", "grains")]
    [InlineData("en:frozen-foods", "frozen")]
    [InlineData("en:milk-substitutes", "plant_based")]
    [InlineData("en:chocolates", "sweets")]
    public void CategoryOf_MapsTag(string tag, string expected) =>
        Assert.Equal(expected, FoodFactsCategoryMap.CategoryOf([tag]));

    [Fact]
    public void CategoryOf_SpecificBeatsGeneral()
    {
        Assert.Equal("cheese", FoodFactsCategoryMap.CategoryOf(["en:dairies", "en:cheeses"]));
        Assert.Equal("plant_based", FoodFactsCategoryMap.CategoryOf(["en:beverages", "en:milk-substitutes"]));
        Assert.Equal("grains", FoodFactsCategoryMap.CategoryOf(["en:plant-based-foods-and-beverages", "en:pastas"]));
    }

    [Fact]
    public void CategoryOf_UnknownOrEmpty_IsNull()
    {
        Assert.Null(FoodFactsCategoryMap.CategoryOf(["en:something-else"]));
        Assert.Null(FoodFactsCategoryMap.CategoryOf([]));
    }

    [Fact]
    public void EveryTarget_IsAKnownCategory()
    {
        var targets = typeof(FoodFactsCategoryMap)
            .GetField("Table", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
            .GetValue(null) as (string Tag, string Category)[];

        Assert.NotNull(targets);
        Assert.All(targets, entry => Assert.Contains(entry.Category, ProductCategories.All));
    }
}
