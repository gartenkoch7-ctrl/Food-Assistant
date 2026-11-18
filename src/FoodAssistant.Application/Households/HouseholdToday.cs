namespace FoodAssistant.Application.Households;

/// <summary>
/// "Today" of the active household: the date in its time zone (AGENTS.md 2.4) and the household's
/// lead time for "expires soon" (<paramref name="ExpiryLeadDays"/>).
/// </summary>
public sealed record HouseholdToday(DateOnly Today, int ExpiryLeadDays, string TimeZoneId);
