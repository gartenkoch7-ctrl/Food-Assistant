using FoodAssistant.Domain.Inventory;

namespace FoodAssistant.Domain.Tests.Inventory;

public sealed class ShelfLifeRuleTests
{
    [Fact]
    public void Empty_HasNoValues()
    {
        var rule = ShelfLifeRule.Empty;

        Assert.Null(rule.DaysAfterOpening);
        Assert.Null(rule.DaysFrozen);
        Assert.Null(rule.DaysAfterThawing);
        Assert.Null(rule.DaysAsLeftover);
    }

    [Fact]
    public void Init_ZeroDays_IsAllowed() =>
        Assert.Equal(0, new ShelfLifeRule { DaysAfterThawing = 0 }.DaysAfterThawing);

    [Fact]
    public void Init_NegativeDaysAfterOpening_ThrowsNamingProperty()
    {
        var error = Assert.Throws<ArgumentOutOfRangeException>(() => new ShelfLifeRule { DaysAfterOpening = -1 });

        Assert.Equal(nameof(ShelfLifeRule.DaysAfterOpening), error.ParamName);
    }

    [Fact]
    public void Init_NegativeDaysFrozen_Throws() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new ShelfLifeRule { DaysFrozen = -1 });

    [Fact]
    public void Init_NegativeDaysAfterThawing_Throws() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new ShelfLifeRule { DaysAfterThawing = -1 });

    [Fact]
    public void Init_NegativeDaysAsLeftover_Throws() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new ShelfLifeRule { DaysAsLeftover = -1 });

    [Fact]
    public void With_NegativeDays_Throws() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => ShelfLifeRule.Empty with { DaysFrozen = -5 });
}
