using System.Text.RegularExpressions;
using FoodAssistant.Domain.Catalog;
using FoodAssistant.Domain.Inventory;

namespace FoodAssistant.Domain.Tests.Catalog;

/// <summary>The fixed category list (M1 decision F1).</summary>
public sealed partial class ProductCategoriesTests
{
    [Fact]
    public void All_HasTwentyFourUniqueKeys()
    {
        Assert.Equal(24, ProductCategories.All.Count);
        Assert.Equal(ProductCategories.All.Count, ProductCategories.All.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void All_KeysAreLowerSnakeCaseAndFitTheColumn()
    {
        Assert.All(ProductCategories.All, key =>
        {
            Assert.Matches(KeyPattern(), key);
            Assert.True(key.Length <= 32, key);
        });
    }

    [Fact]
    public void All_EveryCategoryIsKnownAndHasADefinedStorageType()
    {
        Assert.All(ProductCategories.All, key =>
        {
            Assert.True(ProductCategories.IsKnown(key));
            Assert.True(Enum.IsDefined(ProductCategories.DefaultStorageType(key)));
        });
    }

    [Theory]
    [InlineData(ProductCategories.Dairy, StorageType.Chilled)]
    [InlineData(ProductCategories.Fruit, StorageType.Ambient)]
    [InlineData(ProductCategories.Vegetables, StorageType.Chilled)]
    [InlineData(ProductCategories.Frozen, StorageType.Frozen)]
    [InlineData(ProductCategories.PlantBased, StorageType.Chilled)]
    [InlineData(ProductCategories.Other, StorageType.Ambient)]
    public void DefaultStorageType_FollowsTheTable(string key, StorageType expected) =>
        Assert.Equal(expected, ProductCategories.DefaultStorageType(key));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Dairy")]
    [InlineData("unknown")]
    public void IsKnown_UnknownKey_IsFalse(string? key) => Assert.False(ProductCategories.IsKnown(key));

    [Fact]
    public void DefaultStorageType_UnknownKey_IsAmbient() =>
        Assert.Equal(StorageType.Ambient, ProductCategories.DefaultStorageType("unknown"));

    [GeneratedRegex("^[a-z_]+$")]
    private static partial Regex KeyPattern();
}
