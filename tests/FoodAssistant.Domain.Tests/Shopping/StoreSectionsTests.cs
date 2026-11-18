using FoodAssistant.Domain.Catalog;
using FoodAssistant.Domain.Shopping;

namespace FoodAssistant.Domain.Tests.Shopping;

/// <summary>The fixed store-section list (M1 decision F3).</summary>
public sealed class StoreSectionsTests
{
    [Fact]
    public void All_HasElevenUniqueKeysInDefaultOrder()
    {
        Assert.Equal(11, StoreSections.All.Count);
        Assert.Equal(StoreSections.All.Count, StoreSections.All.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(StoreSections.Produce, StoreSections.All[0]);
        Assert.Equal(StoreSections.Other, StoreSections.All[^1]);
    }

    [Fact]
    public void All_KeysFitTheColumn() =>
        Assert.All(StoreSections.All, key => Assert.True(key.Length <= 32, key));

    [Fact]
    public void ForCategory_EveryCategoryMapsToAKnownSection() =>
        Assert.All(ProductCategories.All, category => Assert.True(StoreSections.IsKnown(StoreSections.ForCategory(category)), category));

    [Theory]
    [InlineData(ProductCategories.Cheese, StoreSections.DairyChilled)]
    [InlineData(ProductCategories.Sausage, StoreSections.MeatFish)]
    [InlineData(ProductCategories.Herbs, StoreSections.Produce)]
    [InlineData(ProductCategories.Bread, StoreSections.Bakery)]
    [InlineData(ProductCategories.Oils, StoreSections.Pantry)]
    [InlineData(ProductCategories.Baking, StoreSections.SpicesBaking)]
    [InlineData(ProductCategories.ReadyMeals, StoreSections.DairyChilled)]
    [InlineData(ProductCategories.Beverages, StoreSections.Beverages)]
    public void ForCategory_FollowsTheTable(string category, string expected) =>
        Assert.Equal(expected, StoreSections.ForCategory(category));

    [Fact]
    public void ForCategory_UnknownCategory_IsOther() =>
        Assert.Equal(StoreSections.Other, StoreSections.ForCategory("unknown"));

    [Theory]
    [InlineData(null)]
    [InlineData("Produce")]
    [InlineData("garden")]
    public void IsKnown_UnknownKey_IsFalse(string? key) => Assert.False(StoreSections.IsKnown(key));
}
