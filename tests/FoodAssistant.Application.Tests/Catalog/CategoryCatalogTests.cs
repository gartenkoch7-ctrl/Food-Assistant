using FoodAssistant.Application.Catalog;
using FoodAssistant.Contracts.Inventory;
using FoodAssistant.Domain.Catalog;
using FoodAssistant.Domain.Shopping;

namespace FoodAssistant.Application.Tests.Catalog;

public sealed class CategoryCatalogTests
{
    [Fact]
    public void All_ListsEveryCategoryInOrder() =>
        Assert.Equal(ProductCategories.All, CategoryCatalog.All.Select(c => c.Key));

    [Fact]
    public void All_CarriesSectionAndStorageTypeDefaults()
    {
        var cheese = Assert.Single(CategoryCatalog.All, c => c.Key == ProductCategories.Cheese);
        var frozen = Assert.Single(CategoryCatalog.All, c => c.Key == ProductCategories.Frozen);

        Assert.Equal((StoreSections.DairyChilled, StorageTypeDto.Chilled), (cheese.DefaultSectionKey, cheese.DefaultStorageType));
        Assert.Equal((StoreSections.Frozen, StorageTypeDto.Frozen), (frozen.DefaultSectionKey, frozen.DefaultStorageType));
    }

    [Fact]
    public void All_EverySectionIsKnown() =>
        Assert.All(CategoryCatalog.All, c => Assert.True(StoreSections.IsKnown(c.DefaultSectionKey)));
}
