using FoodAssistant.Domain.Quantities;

namespace FoodAssistant.Domain.Tests.Quantities;

public sealed class UnitTests
{
    [Theory]
    [InlineData("g", UnitDimension.Mass, false)]
    [InlineData("kg", UnitDimension.Mass, false)]
    [InlineData("ml", UnitDimension.Volume, false)]
    [InlineData("l", UnitDimension.Volume, false)]
    [InlineData("piece", UnitDimension.Count, false)]
    [InlineData("tbsp", UnitDimension.Volume, true)]
    [InlineData("tsp", UnitDimension.Volume, true)]
    [InlineData("pinch", UnitDimension.None, false)]
    [InlineData("bunch", UnitDimension.None, false)]
    [InlineData("package", UnitDimension.None, false)]
    [InlineData("can", UnitDimension.None, false)]
    public void FromCode_KnownCode_ReturnsUnitWithDimension(string code, UnitDimension dimension, bool approximate)
    {
        var unit = Unit.FromCode(code);

        Assert.Equal(code, unit.Code);
        Assert.Equal(dimension, unit.Dimension);
        Assert.Equal(approximate, unit.IsApproximate);
        Assert.Equal(code, unit.ToString());
    }

    [Fact]
    public void FromCode_SameCodeTwice_ReturnsSameInstance() =>
        Assert.Same(Unit.FromCode("kg"), Unit.Kilogram);

    [Theory]
    [InlineData("oz")]
    [InlineData("KG")]
    [InlineData("")]
    public void FromCode_UnknownCode_Throws(string code) =>
        Assert.Throws<ArgumentException>(() => Unit.FromCode(code));

    [Fact]
    public void TryFromCode_Null_ReturnsFalse()
    {
        Assert.False(Unit.TryFromCode(null, out var unit));
        Assert.Null(unit);
    }

    [Fact]
    public void All_ContainsElevenDistinctCodes() =>
        Assert.Equal(11, Unit.All.Select(u => u.Code).Distinct().Count());

    [Theory]
    [InlineData("g", "kg", true)]
    [InlineData("ml", "l", true)]
    [InlineData("tbsp", "ml", true)]
    [InlineData("tsp", "tbsp", true)]
    [InlineData("piece", "piece", true)]
    [InlineData("pinch", "pinch", true)]
    [InlineData("g", "ml", false)]
    [InlineData("piece", "g", false)]
    [InlineData("pinch", "bunch", false)]
    [InlineData("package", "can", false)]
    [InlineData("can", "piece", false)]
    public void CanConvertTo_UnitPair_MatchesDimensionRule(string from, string to, bool expected) =>
        Assert.Equal(expected, Unit.FromCode(from).CanConvertTo(Unit.FromCode(to)));

    [Fact]
    public void CanConvertTo_Null_Throws() =>
        Assert.Throws<ArgumentNullException>(() => Unit.Gram.CanConvertTo(null!));
}
