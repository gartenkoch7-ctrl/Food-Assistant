using FoodAssistant.Domain.Inventory;

namespace FoodAssistant.Domain.Tests.Inventory;

public sealed class ShelfLifeRuleResolverTests
{
    private static readonly ShelfLifeRule Default = new()
    {
        DaysAfterOpening = 5,
        DaysFrozen = 180,
        DaysAfterThawing = 2,
        DaysAsLeftover = 3,
    };

    [Fact]
    public void Resolve_OnlyDefault_ReturnsDefaultValues() =>
        Assert.Equal(Default, ShelfLifeRuleResolver.Resolve(null, null, Default));

    [Fact]
    public void Resolve_CategoryOverride_BeatsDefault()
    {
        var category = new ShelfLifeRule { DaysAfterOpening = 4 };

        var rule = ShelfLifeRuleResolver.Resolve(null, category, Default);

        Assert.Equal(4, rule.DaysAfterOpening);
    }

    [Fact]
    public void Resolve_ProductOverride_BeatsCategoryAndDefault()
    {
        var product = new ShelfLifeRule { DaysAfterOpening = 1 };
        var category = new ShelfLifeRule { DaysAfterOpening = 4 };

        var rule = ShelfLifeRuleResolver.Resolve(product, category, Default);

        Assert.Equal(1, rule.DaysAfterOpening);
    }

    [Fact]
    public void Resolve_PartialOverrides_FallBackPerValue()
    {
        var product = new ShelfLifeRule { DaysFrozen = 60 };
        var category = new ShelfLifeRule { DaysAfterThawing = 1, DaysFrozen = 120 };

        var rule = ShelfLifeRuleResolver.Resolve(product, category, Default);

        Assert.Equal(5, rule.DaysAfterOpening);
        Assert.Equal(60, rule.DaysFrozen);
        Assert.Equal(1, rule.DaysAfterThawing);
        Assert.Equal(3, rule.DaysAsLeftover);
    }

    [Fact]
    public void Resolve_NoRuleAnywhere_ReturnsEmptyRule() =>
        Assert.Equal(ShelfLifeRule.Empty, ShelfLifeRuleResolver.Resolve(null, null, null));

    [Fact]
    public void Resolve_OverrideWithoutDefault_UsesOverride()
    {
        var category = new ShelfLifeRule { DaysAsLeftover = 2 };

        var rule = ShelfLifeRuleResolver.Resolve(null, category, null);

        Assert.Equal(2, rule.DaysAsLeftover);
        Assert.Null(rule.DaysAfterOpening);
    }
}
