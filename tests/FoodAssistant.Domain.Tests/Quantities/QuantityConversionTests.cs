using FoodAssistant.Domain.Quantities;

namespace FoodAssistant.Domain.Tests.Quantities;

public sealed class QuantityConversionTests
{
    [Fact]
    public void Constructor_NegativeValue_Throws() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new Quantity(-0.1m, Unit.Gram));

    [Fact]
    public void Constructor_NullUnit_Throws() =>
        Assert.Throws<ArgumentNullException>(() => new Quantity(1m, null!));

    [Fact]
    public void Zero_AnyUnit_IsZero()
    {
        var zero = Quantity.Zero(Unit.Litre);

        Assert.True(zero.IsZero);
        Assert.Same(Unit.Litre, zero.Unit);
    }

    [Theory]
    [InlineData(1.5, "kg", "g", 1500)]
    [InlineData(250, "g", "kg", 0.25)]
    [InlineData(2, "l", "ml", 2000)]
    [InlineData(500, "ml", "l", 0.5)]
    [InlineData(2, "tbsp", "ml", 30)]
    [InlineData(3, "tsp", "ml", 15)]
    [InlineData(3, "tsp", "tbsp", 1)]
    [InlineData(45, "ml", "tbsp", 3)]
    [InlineData(4, "piece", "piece", 4)]
    [InlineData(2, "pinch", "pinch", 2)]
    public void TryConvertTo_SameDimension_Converts(decimal value, string from, string to, decimal expected)
    {
        var quantity = new Quantity(value, Unit.FromCode(from));

        Assert.True(quantity.TryConvertTo(Unit.FromCode(to), out var result));
        Assert.Equal(expected, result.Value);
        Assert.Same(Unit.FromCode(to), result.Unit);
    }

    [Theory]
    [InlineData("g", "ml")]
    [InlineData("piece", "kg")]
    [InlineData("pinch", "tsp")]
    [InlineData("bunch", "package")]
    public void TryConvertTo_IncompatibleUnit_ReturnsFalse(string from, string to)
    {
        var quantity = new Quantity(1m, Unit.FromCode(from));

        Assert.False(quantity.TryConvertTo(Unit.FromCode(to), out var result));
        Assert.Equal(default, result);
    }

    [Fact]
    public void ConvertTo_IncompatibleUnit_Throws() =>
        Assert.Throws<InvalidOperationException>(() => new Quantity(1m, Unit.Can).ConvertTo(Unit.Piece));

    [Fact]
    public void ConvertTo_CompatibleUnit_ReturnsConverted() =>
        Assert.Equal(new Quantity(1200m, Unit.Gram), new Quantity(1.2m, Unit.Kilogram).ConvertTo(Unit.Gram));

    [Theory]
    [InlineData("tbsp", "ml", true)]
    [InlineData("ml", "tsp", true)]
    [InlineData("tbsp", "tbsp", false)]
    [InlineData("g", "kg", false)]
    public void IsApproximateIn_HouseholdMeasure_FlagsApproximation(string from, string to, bool expected) =>
        Assert.Equal(expected, new Quantity(1m, Unit.FromCode(from)).IsApproximateIn(Unit.FromCode(to)));

    [Fact]
    public void IsApproximateIn_NullUnit_Throws() =>
        Assert.Throws<ArgumentNullException>(() => new Quantity(1m, Unit.Gram).IsApproximateIn(null!));

    [Theory]
    [InlineData(1, "kg", 999, "g", 1)]
    [InlineData(1, "kg", 1000, "g", 0)]
    [InlineData(1, "kg", 1001, "g", -1)]
    [InlineData(1, "tbsp", 3, "tsp", 0)]
    [InlineData(2, "piece", 3, "piece", -1)]
    public void CompareTo_ConvertibleUnits_ComparesAmounts(
        decimal leftValue, string leftUnit, decimal rightValue, string rightUnit, int expected)
    {
        var left = new Quantity(leftValue, Unit.FromCode(leftUnit));
        var right = new Quantity(rightValue, Unit.FromCode(rightUnit));

        Assert.Equal(expected, Math.Sign(left.CompareTo(right)));
        Assert.Equal(expected < 0, left < right);
        Assert.Equal(expected > 0, left > right);
        Assert.Equal(expected <= 0, left <= right);
        Assert.Equal(expected >= 0, left >= right);
    }

    [Fact]
    public void CompareTo_IncompatibleUnits_Throws() =>
        Assert.Throws<InvalidOperationException>(() => new Quantity(1m, Unit.Gram).CompareTo(new Quantity(1m, Unit.Millilitre)));

    [Fact]
    public void Equals_SameAmountDifferentUnit_IsStructural()
    {
        var kilogram = new Quantity(1m, Unit.Kilogram);
        var grams = new Quantity(1000m, Unit.Gram);

        Assert.NotEqual(kilogram, grams);
        Assert.Equal(0, kilogram.CompareTo(grams));
        Assert.Equal(new Quantity(1m, Unit.Kilogram), kilogram);
    }
}
