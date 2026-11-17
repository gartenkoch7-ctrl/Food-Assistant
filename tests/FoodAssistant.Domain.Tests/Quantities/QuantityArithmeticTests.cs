using FoodAssistant.Domain.Quantities;

namespace FoodAssistant.Domain.Tests.Quantities;

public sealed class QuantityArithmeticTests
{
    [Theory]
    [InlineData(1, "kg", 500, "g", 1.5, "kg")]
    [InlineData(500, "g", 1, "kg", 1500, "g")]
    [InlineData(1, "l", 250, "ml", 1.25, "l")]
    [InlineData(1, "tbsp", 1, "tsp", 20, "ml")]
    [InlineData(2, "piece", 3, "piece", 5, "piece")]
    [InlineData(1, "pinch", 2, "pinch", 3, "pinch")]
    public void Add_SameDimension_ReturnsSumInLeftUnit(
        decimal leftValue, string leftUnit, decimal rightValue, string rightUnit, decimal expectedValue, string expectedUnit)
    {
        var sum = new Quantity(leftValue, Unit.FromCode(leftUnit)).Add(new Quantity(rightValue, Unit.FromCode(rightUnit)));

        Assert.Equal(0, sum.CompareTo(new Quantity(expectedValue, Unit.FromCode(expectedUnit))));
        Assert.Same(Unit.FromCode(leftUnit), sum.Unit);
    }

    [Theory]
    [InlineData("g", "ml")]
    [InlineData("piece", "g")]
    [InlineData("pinch", "bunch")]
    [InlineData("can", "package")]
    public void Add_IncompatibleUnits_Throws(string left, string right) =>
        Assert.Throws<InvalidOperationException>(
            () => new Quantity(1m, Unit.FromCode(left)).Add(new Quantity(1m, Unit.FromCode(right))));

    [Theory]
    [InlineData(1, "kg", 200, "g", 0.8)]
    [InlineData(1, "kg", 1000, "g", 0)]
    [InlineData(500, "ml", 0.5, "l", 0)]
    [InlineData(1, "tbsp", 3, "tsp", 0)]
    [InlineData(5, "piece", 2, "piece", 3)]
    [InlineData(2, "can", 2, "can", 0)]
    public void Subtract_NotMoreThanAvailable_ReturnsDifferenceInLeftUnit(
        decimal leftValue, string leftUnit, decimal rightValue, string rightUnit, decimal expected)
    {
        var left = new Quantity(leftValue, Unit.FromCode(leftUnit));

        var difference = left.Subtract(new Quantity(rightValue, Unit.FromCode(rightUnit)));

        Assert.Equal(expected, difference.Value);
        Assert.Same(left.Unit, difference.Unit);
    }

    [Theory]
    [InlineData(1, "kg", 1001, "g")]
    [InlineData(1, "piece", 2, "piece")]
    [InlineData(1, "tbsp", 16, "ml")]
    public void Subtract_MoreThanAvailable_Throws(decimal leftValue, string leftUnit, decimal rightValue, string rightUnit) =>
        Assert.Throws<InvalidOperationException>(
            () => new Quantity(leftValue, Unit.FromCode(leftUnit)).Subtract(new Quantity(rightValue, Unit.FromCode(rightUnit))));

    [Fact]
    public void Subtract_IncompatibleUnits_Throws() =>
        Assert.Throws<InvalidOperationException>(
            () => new Quantity(1m, Unit.Bunch).Subtract(new Quantity(1m, Unit.Pinch)));
}
