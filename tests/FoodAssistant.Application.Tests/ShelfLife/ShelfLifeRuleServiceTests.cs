using FoodAssistant.Application.ShelfLife;
using FoodAssistant.Domain.Catalog;
using FoodAssistant.Domain.Inventory;

namespace FoodAssistant.Application.Tests.ShelfLife;

public sealed class ShelfLifeRuleServiceTests
{
    private static readonly Guid Milk = Guid.CreateVersion7();

    private static readonly ShelfLifeRule DairyDefault = new()
    {
        DaysAfterOpening = 3,
        DaysFrozen = 90,
        DaysAfterThawing = 1,
        DaysAsLeftover = 2,
    };

    private readonly FakeShelfLifeOverrideStore _store = new();

    public ShelfLifeRuleServiceTests() => _store.ProductCategories[Milk] = ProductCategories.Dairy;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task WithoutAnyRule_ReturnsEmpty()
    {
        var service = new ShelfLifeRuleService(_store, new FakeDefaultShelfLifeRules());

        Assert.Equal(ShelfLifeRule.Empty, await service.ResolveAsync(Milk, StorageType.Chilled, Token));
        Assert.Equal(ShelfLifeRule.Empty, await service.ResolveForCategoryAsync(ProductCategories.Dairy, StorageType.Chilled, Token));
    }

    [Fact]
    public async Task UnknownProduct_ReturnsEmptyEvenWithDefaults()
    {
        var service = new ShelfLifeRuleService(_store, Defaults());

        Assert.Equal(ShelfLifeRule.Empty, await service.ResolveAsync(Guid.CreateVersion7(), StorageType.Chilled, Token));
    }

    [Fact]
    public async Task OnlyDefault_ReturnsDefault()
    {
        var service = new ShelfLifeRuleService(_store, Defaults());

        Assert.Equal(DairyDefault, await service.ResolveAsync(Milk, StorageType.Chilled, Token));
    }

    [Fact]
    public async Task DefaultOfOtherStorageType_DoesNotApply()
    {
        var service = new ShelfLifeRuleService(_store, Defaults());

        Assert.Equal(ShelfLifeRule.Empty, await service.ResolveAsync(Milk, StorageType.Ambient, Token));
    }

    [Fact]
    public async Task ProductBeatsCategoryBeatsDefault_PerValue()
    {
        _store.Categories[(ProductCategories.Dairy, StorageType.Chilled)] = new ShelfLifeRule { DaysAfterOpening = 2, DaysFrozen = 60 };
        _store.Products[(Milk, StorageType.Chilled)] = new ShelfLifeRule { DaysAfterOpening = 5 };
        var service = new ShelfLifeRuleService(_store, Defaults());

        var rule = await service.ResolveAsync(Milk, StorageType.Chilled, Token);

        Assert.Equal(new ShelfLifeRule { DaysAfterOpening = 5, DaysFrozen = 60, DaysAfterThawing = 1, DaysAsLeftover = 2 }, rule);
    }

    [Fact]
    public async Task ResolveForCategory_IgnoresProductOverrides()
    {
        _store.Categories[(ProductCategories.Dairy, StorageType.Chilled)] = new ShelfLifeRule { DaysAfterOpening = 2 };
        _store.Products[(Milk, StorageType.Chilled)] = new ShelfLifeRule { DaysAfterOpening = 5 };
        var service = new ShelfLifeRuleService(_store, Defaults());

        var rule = await service.ResolveForCategoryAsync(ProductCategories.Dairy, StorageType.Chilled, Token);

        Assert.Equal(DairyDefault with { DaysAfterOpening = 2 }, rule);
    }

    [Fact]
    public async Task OverrideWithoutDefault_StillApplies()
    {
        _store.Products[(Milk, StorageType.Frozen)] = new ShelfLifeRule { DaysFrozen = 30 };
        var service = new ShelfLifeRuleService(_store, new FakeDefaultShelfLifeRules());

        Assert.Equal(new ShelfLifeRule { DaysFrozen = 30 }, await service.ResolveAsync(Milk, StorageType.Frozen, Token));
    }

    private static FakeDefaultShelfLifeRules Defaults() =>
        new(new DefaultShelfLifeRow(ProductCategories.Dairy, StorageType.Chilled, DairyDefault, new ShelfLifeRuleSources("test", "test", "test", "test")));
}
