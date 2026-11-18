namespace FoodAssistant.Domain.Shopping;

/// <summary>Due dates of recurring shopping rules (docs/kb/einkauf.md). All dates are household dates.</summary>
public static class RecurrenceSchedule
{
    /// <summary>Whether the weekday / day of month fit the frequency (daily ignores both).</summary>
    public static bool IsValid(RecurrenceFrequency frequency, DayOfWeek? dayOfWeek, int? dayOfMonth) =>
        frequency switch
        {
            RecurrenceFrequency.Daily => true,
            RecurrenceFrequency.Weekly => dayOfWeek is { } day && Enum.IsDefined(day),
            RecurrenceFrequency.Monthly => dayOfMonth is >= 1 and <= 31,
            _ => false,
        };

    /// <summary>
    /// The first due date strictly after <paramref name="after"/>. Monthly rules on a day the month
    /// does not have fall on its last day (31 → 30./28./29.).
    /// </summary>
    /// <exception cref="ArgumentException">The weekday / day of month do not fit the frequency.</exception>
    public static DateOnly NextDue(RecurrenceFrequency frequency, DayOfWeek? dayOfWeek, int? dayOfMonth, DateOnly after)
    {
        if (!IsValid(frequency, dayOfWeek, dayOfMonth))
        {
            throw new ArgumentException("The weekday or day of month does not fit the frequency.");
        }

        return frequency switch
        {
            RecurrenceFrequency.Daily => after.AddDays(1),
            RecurrenceFrequency.Weekly => NextWeekday(dayOfWeek!.Value, after),
            _ => NextMonthDay(dayOfMonth!.Value, after),
        };
    }

    // 1..7 days ahead: a due date equal to "after" does not count.
    private static DateOnly NextWeekday(DayOfWeek target, DateOnly after)
    {
        var days = ((int)target - (int)after.DayOfWeek + 7) % 7;
        return after.AddDays(days == 0 ? 7 : days);
    }

    private static DateOnly NextMonthDay(int dayOfMonth, DateOnly after)
    {
        var inThisMonth = InMonth(after.Year, after.Month, dayOfMonth);
        if (inThisMonth > after)
        {
            return inThisMonth;
        }

        var next = new DateOnly(after.Year, after.Month, 1).AddMonths(1);
        return InMonth(next.Year, next.Month, dayOfMonth);
    }

    private static DateOnly InMonth(int year, int month, int dayOfMonth) =>
        new(year, month, Math.Min(dayOfMonth, DateTime.DaysInMonth(year, month)));
}
