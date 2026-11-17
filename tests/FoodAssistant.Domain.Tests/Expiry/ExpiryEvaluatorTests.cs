using FoodAssistant.Domain.Expiry;

namespace FoodAssistant.Domain.Tests.Expiry;

/// <summary>Status table of docs/kb/vorrat-und-haltbarkeit.md.</summary>
public sealed class ExpiryEvaluatorTests
{
    private static readonly DateOnly Today = new(2026, 11, 18);

    [Theory]
    [InlineData(-1, 1)]
    [InlineData(-3, 3)]
    [InlineData(-400, 400)]
    public void Evaluate_DateBeforeToday_IsExpiredWithDaysSince(int offset, int daysSince)
    {
        var result = ExpiryEvaluator.Evaluate(Today.AddDays(offset), Today);

        Assert.Equal(ExpiryStatus.Expired, result.Status);
        Assert.Equal(offset, result.DaysUntilExpiry);
        Assert.Equal(daysSince, result.DaysSinceExpiry);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Evaluate_DateWithinDefaultLead_IsSoon(int offset)
    {
        var result = ExpiryEvaluator.Evaluate(Today.AddDays(offset), Today);

        Assert.Equal(ExpiryStatus.Soon, result.Status);
        Assert.Equal(offset, result.DaysUntilExpiry);
        Assert.Null(result.DaysSinceExpiry);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(6)]
    [InlineData(365)]
    public void Evaluate_DateAfterDefaultLead_IsOk(int offset)
    {
        var result = ExpiryEvaluator.Evaluate(Today.AddDays(offset), Today);

        Assert.Equal(ExpiryStatus.Ok, result.Status);
        Assert.Equal(offset, result.DaysUntilExpiry);
        Assert.Null(result.DaysSinceExpiry);
    }

    [Fact]
    public void Evaluate_NoDate_IsNoDate()
    {
        var result = ExpiryEvaluator.Evaluate(null, Today);

        Assert.Equal(ExpiryStatus.NoDate, result.Status);
        Assert.Null(result.DaysUntilExpiry);
        Assert.Null(result.DaysSinceExpiry);
    }

    [Theory]
    [InlineData(0, 0, ExpiryStatus.Soon)]
    [InlineData(0, 1, ExpiryStatus.Ok)]
    [InlineData(5, 5, ExpiryStatus.Soon)]
    [InlineData(5, 6, ExpiryStatus.Ok)]
    [InlineData(5, -1, ExpiryStatus.Expired)]
    public void Evaluate_CustomLead_UsesLeadAsUpperBound(int leadDays, int offset, ExpiryStatus expected) =>
        Assert.Equal(expected, ExpiryEvaluator.Evaluate(Today.AddDays(offset), Today, leadDays).Status);

    [Fact]
    public void Evaluate_NegativeLead_Throws() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => ExpiryEvaluator.Evaluate(Today, Today, -1));

    [Fact]
    public void Evaluate_AcrossYearEnd_CountsCalendarDays()
    {
        var result = ExpiryEvaluator.Evaluate(new DateOnly(2027, 1, 1), new DateOnly(2026, 12, 30));

        Assert.Equal(2, result.DaysUntilExpiry);
        Assert.Equal(ExpiryStatus.Soon, result.Status);
    }
}
