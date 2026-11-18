using FoodAssistant.Application.Common;
using FoodAssistant.Application.Households;
using FoodAssistant.Application.ShelfLife;
using FoodAssistant.Contracts.Catalog;
using FoodAssistant.Contracts.Households;
using FoodAssistant.Contracts.Inventory;
using FoodAssistant.Contracts.ShelfLife;
using FoodAssistant.Domain.Catalog;
using FoodAssistant.Domain.Inventory;
using Microsoft.Extensions.Logging.Abstractions;

namespace FoodAssistant.Application.Tests.ShelfLife;

/// <summary>Default, own and effective values with a non-empty default table (the endpoint tests run with the shipped, empty one).</summary>
public sealed class ShelfLifeAdminServiceTests
{
    private static readonly Guid Household = Guid.CreateVersion7();
    private static readonly Guid Milk = Guid.CreateVersion7();

    private readonly FakeShelfLifeOverrideStore _store = new();
    private readonly FakeDefaultShelfLifeRules _defaults = new(new DefaultShelfLifeRow(
        ProductCategories.Dairy,
        StorageType.Chilled,
        new ShelfLifeRule { DaysAfterOpening = 3, DaysAsLeftover = 2 },
        new ShelfLifeRuleSources("test", null, null, "test")));

    public ShelfLifeAdminServiceTests() => _store.ProductCategories[Milk] = ProductCategories.Dairy;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ListRules_ShowsDefaultOwnAndEffectiveWithSource()
    {
        _store.Categories[(ProductCategories.Dairy, StorageType.Chilled)] = new ShelfLifeRule { DaysAfterOpening = 1 };

        var rows = await Admin().ListRulesAsync(Household, Token);

        var dairy = Assert.Single(rows, r => r.CategoryKey == ProductCategories.Dairy && r.StorageType == StorageTypeDto.Chilled);
        Assert.Equal(new ShelfLifeDaysDto(3, null, null, 2), dairy.Default);
        Assert.Equal(new ShelfLifeDaysDto(1, null, null, null), dairy.Override);
        Assert.Equal(new ShelfLifeDaysDto(1, null, null, 2), dairy.Effective);
        Assert.Equal(new ShelfLifeSourceKeysDto("test", null, null, "test"), dairy.DefaultSources);
        var frozen = Assert.Single(rows, r => r.CategoryKey == ProductCategories.Dairy && r.StorageType == StorageTypeDto.Frozen);
        Assert.Equal(new ShelfLifeSourceKeysDto(null, null, null, null), frozen.DefaultSources);
        Assert.Equal(ProductCategories.All, rows.Select(r => r.CategoryKey).Distinct());
    }

    [Fact]
    public void ListSources_MapsTheTableSources()
    {
        var source = Assert.Single(Admin().ListSources());

        Assert.Equal(new ShelfLifeSourceDto("test", "Test source", "https://example.org/", new DateOnly(2026, 11, 18)), source);
    }

    [Fact]
    public async Task ProductRow_InheritsCategoryEffectiveValues()
    {
        _store.Categories[(ProductCategories.Dairy, StorageType.Chilled)] = new ShelfLifeRule { DaysAfterOpening = 1 };

        var result = await Products().SetOverrideAsync(Household, Milk, "chilled", new ShelfLifeDaysDto(null, null, null, 4), Token);

        Assert.Equal(OperationStatus.Ok, result.Status);
        Assert.Equal(new ShelfLifeDaysDto(1, null, null, 2), result.Value!.Default);
        Assert.Equal(new ShelfLifeDaysDto(1, null, null, 4), result.Value.Effective);

        // The opening value now comes from the household's category override, so only the leftover cites the source.
        Assert.Equal(new ShelfLifeSourceKeysDto(null, null, null, "test"), result.Value.DefaultSources);
    }

    [Fact]
    public async Task UnknownProduct_IsNotFound()
    {
        var result = await Products().GetAsync(Household, Guid.CreateVersion7(), Token);

        Assert.Equal((OperationStatus.NotFound, CatalogErrorCodes.ProductNotFound), (result.Status, result.ErrorCode));
    }

    [Fact]
    public async Task OtherHousehold_IsAProgrammingError()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => Admin().ListRulesAsync(Guid.CreateVersion7(), Token));
    }

    private ShelfLifeAdminService Admin() =>
        new(_store, _defaults, new Context(), TimeProvider.System, NullLogger<ShelfLifeAdminService>.Instance);

    private ProductShelfLifeService Products() =>
        new(_store, _defaults, new Context(), TimeProvider.System, NullLogger<ProductShelfLifeService>.Instance);

    private sealed class Context : IHouseholdContext
    {
        public Guid? HouseholdId => Household;

        public HouseholdMembershipInfo? Membership => new(Household, Guid.Empty, HouseholdRole.Owner);
    }
}
