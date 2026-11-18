namespace FoodAssistant.Application.Households;

/// <summary>
/// The only source of "today" for household data: expiry status, consumption dates, due dates.
/// Never use <c>DateTime.Today</c> or the server's time zone.
/// </summary>
public interface IHouseholdCalendar
{
    /// <summary>Today in the time zone of the active household.</summary>
    /// <exception cref="InvalidOperationException">No household is active (programming error).</exception>
    Task<HouseholdToday> GetTodayAsync(CancellationToken cancellationToken);
}
