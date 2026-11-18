using FoodAssistant.Domain.Shopping;

namespace FoodAssistant.Domain.Tests.Shopping;

public sealed class RecurrenceScheduleTests
{
    private static DateOnly D(string iso) => DateOnly.ParseExact(iso, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

    [Fact]
    public void NextDue_Daily_IsTomorrow() =>
        Assert.Equal(new DateOnly(2026, 12, 1), RecurrenceSchedule.NextDue(RecurrenceFrequency.Daily, null, null, new DateOnly(2026, 11, 30)));

    [Theory]
    [InlineData("2026-11-18", DayOfWeek.Thursday, "2026-11-19")]
    [InlineData("2026-11-18", DayOfWeek.Wednesday, "2026-11-25")]
    [InlineData("2026-11-18", DayOfWeek.Monday, "2026-11-23")]
    [InlineData("2026-12-31", DayOfWeek.Friday, "2027-01-01")]
    public void NextDue_Weekly_IsTheNextMatchingWeekdayAfterTheGivenDay(string after, DayOfWeek day, string expected) =>
        Assert.Equal(D(expected), RecurrenceSchedule.NextDue(RecurrenceFrequency.Weekly, day, null, D(after)));

    [Theory]
    [InlineData("2026-11-18", 20, "2026-11-20")]
    [InlineData("2026-11-20", 20, "2026-12-20")]
    [InlineData("2026-11-30", 1, "2026-12-01")]
    [InlineData("2026-12-31", 15, "2027-01-15")]
    [InlineData("2026-01-31", 31, "2026-02-28")]
    [InlineData("2026-01-15", 31, "2026-01-31")]
    [InlineData("2026-03-31", 31, "2026-04-30")]
    [InlineData("2028-01-31", 30, "2028-02-29")]
    public void NextDue_Monthly_ClampsToTheLastDayOfShortMonths(string after, int day, string expected) =>
        Assert.Equal(D(expected), RecurrenceSchedule.NextDue(RecurrenceFrequency.Monthly, null, day, D(after)));

    [Fact]
    public void NextDue_FirstDueDateOfANewRule_CountsToday()
    {
        var today = new DateOnly(2026, 11, 18);

        Assert.Equal(today, RecurrenceSchedule.NextDue(RecurrenceFrequency.Weekly, DayOfWeek.Wednesday, null, today.AddDays(-1)));
        Assert.Equal(today, RecurrenceSchedule.NextDue(RecurrenceFrequency.Daily, null, null, today.AddDays(-1)));
        Assert.Equal(today, RecurrenceSchedule.NextDue(RecurrenceFrequency.Monthly, null, 18, today.AddDays(-1)));
    }

    [Theory]
    [InlineData(RecurrenceFrequency.Daily, null, null, true)]
    [InlineData(RecurrenceFrequency.Weekly, DayOfWeek.Monday, null, true)]
    [InlineData(RecurrenceFrequency.Weekly, null, null, false)]
    [InlineData(RecurrenceFrequency.Weekly, (DayOfWeek)9, null, false)]
    [InlineData(RecurrenceFrequency.Monthly, null, 31, true)]
    [InlineData(RecurrenceFrequency.Monthly, null, 1, true)]
    [InlineData(RecurrenceFrequency.Monthly, null, 0, false)]
    [InlineData(RecurrenceFrequency.Monthly, null, 32, false)]
    [InlineData(RecurrenceFrequency.Monthly, null, null, false)]
    public void IsValid_RequiresTheFieldOfTheFrequency(RecurrenceFrequency frequency, DayOfWeek? day, int? dayOfMonth, bool expected) =>
        Assert.Equal(expected, RecurrenceSchedule.IsValid(frequency, day, dayOfMonth));

    [Fact]
    public void NextDue_InvalidCombination_Throws() =>
        Assert.Throws<ArgumentException>(() =>
            RecurrenceSchedule.NextDue(RecurrenceFrequency.Weekly, null, null, new DateOnly(2026, 11, 18)));
}
