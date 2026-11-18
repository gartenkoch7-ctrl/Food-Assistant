using FoodAssistant.Domain.Inventory;
using FoodAssistant.Domain.Quantities;

namespace FoodAssistant.Domain.Tests.Inventory;

public sealed class RestockDecisionTests
{
    private static readonly Quantity TwoLitres = new(2m, Unit.Litre);

    [Theory]
    [InlineData(true, RestockKind.Disarm)]
    [InlineData(false, RestockKind.None)]
    public void Decide_NoMinimum_OnlyForgetsAnOldMemory(bool triggered, RestockKind expected) =>
        Assert.Equal(expected, RestockDecision.Decide(null, Quantity.Zero(Unit.Piece), triggered, onOpenList: false).Kind);

    [Fact]
    public void Decide_BelowMinimum_AddsTheMissingAmount()
    {
        var action = RestockDecision.Decide(TwoLitres, new Quantity(500m, Unit.Millilitre), triggered: false, onOpenList: false);

        Assert.Equal(RestockKind.AddToList, action.Kind);
        Assert.Equal(new Quantity(1.5m, Unit.Litre), action.Amount);
    }

    [Fact]
    public void Decide_BelowMinimum_PiecesAreRoundedUp()
    {
        var action = RestockDecision.Decide(new Quantity(3m, Unit.Piece), new Quantity(0.5m, Unit.Piece), false, false);

        Assert.Equal(new Quantity(3m, Unit.Piece), action.Amount);
    }

    [Theory]
    [InlineData("package")]
    [InlineData("can")]
    [InlineData("bunch")]
    public void Decide_BelowMinimum_OtherCountableUnitsAreRoundedUp(string code)
    {
        var unit = Unit.FromCode(code);

        var action = RestockDecision.Decide(new Quantity(2m, unit), new Quantity(0.5m, unit), false, false);

        Assert.Equal(new Quantity(2m, unit), action.Amount);
    }

    [Fact]
    public void Decide_BelowMinimum_ConversionRemainderIsRoundedUpToThreeDecimals()
    {
        var action = RestockDecision.Decide(new Quantity(1m, Unit.Kilogram), new Quantity(0.5m, Unit.Gram), false, false);

        Assert.Equal(new Quantity(1m, Unit.Kilogram), action.Amount);
    }

    [Fact]
    public void Decide_AboveMinimumWithoutTrigger_DoesNothing() =>
        Assert.Equal(
            RestockKind.None,
            RestockDecision.Decide(TwoLitres, new Quantity(5m, Unit.Litre), triggered: false, onOpenList: false).Kind);

    [Fact]
    public void Decide_BelowMinimum_MeasuredAmountsStayExact()
    {
        var action = RestockDecision.Decide(new Quantity(500m, Unit.Gram), new Quantity(120.5m, Unit.Gram), false, false);

        Assert.Equal(new Quantity(379.5m, Unit.Gram), action.Amount);
    }

    [Fact]
    public void Decide_StockInUnitThatDoesNotConvert_CountsAsNothingAndAsksForTheMinimum()
    {
        var action = RestockDecision.Decide(new Quantity(2m, Unit.Piece), new Quantity(5m, Unit.Gram), false, false);

        Assert.Equal(RestockKind.AddToList, action.Kind);
        Assert.Equal(new Quantity(2m, Unit.Piece), action.Amount);
    }

    [Fact]
    public void Decide_BelowMinimumButAlreadyOnOpenList_OnlyArms()
    {
        var action = RestockDecision.Decide(TwoLitres, Quantity.Zero(Unit.Litre), triggered: false, onOpenList: true);

        Assert.Equal(RestockKind.Arm, action.Kind);
        Assert.Null(action.Amount);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Decide_BelowMinimumAndAlreadyTriggered_DoesNothing(bool onOpenList) =>
        Assert.Equal(
            RestockKind.None,
            RestockDecision.Decide(TwoLitres, Quantity.Zero(Unit.Litre), triggered: true, onOpenList).Kind);

    [Theory]
    [InlineData(true, RestockKind.Disarm)]
    [InlineData(false, RestockKind.None)]
    public void Decide_AtMinimum_IsEnoughAndRearmsOnlyAfterATrigger(bool triggered, RestockKind expected) =>
        Assert.Equal(expected, RestockDecision.Decide(TwoLitres, new Quantity(2000m, Unit.Millilitre), triggered, onOpenList: true).Kind);
}
