using FoodAssistant.Application.Households;
using FoodAssistant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FoodAssistant.Infrastructure.Households;

/// <summary>
/// "Today" in the time zone of the active household. The household settings are read once per
/// scope; the date itself is computed on every call, so a long-running scope crosses midnight correctly.
/// </summary>
internal sealed class HouseholdCalendar(AppDbContext db, IHouseholdContext householdContext, TimeProvider time)
    : IHouseholdCalendar
{
    private (Guid HouseholdId, TimeZoneInfo TimeZone, string TimeZoneId, int ExpiryLeadDays)? _settings;

    public async Task<HouseholdToday> GetTodayAsync(CancellationToken cancellationToken)
    {
        var householdId = householdContext.HouseholdId
            ?? throw new InvalidOperationException("\"Today\" needs an active household.");

        if (_settings is not { } settings || settings.HouseholdId != householdId)
        {
            var household = await db.Households
                .AsNoTracking()
                .Where(h => h.Id == householdId)
                .Select(h => new { h.TimeZoneId, h.ExpiryLeadDays })
                .SingleOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false)
                ?? throw new InvalidOperationException("The active household does not exist or was deleted.");

            settings = (householdId, TimeZoneInfo.FindSystemTimeZoneById(household.TimeZoneId), household.TimeZoneId, household.ExpiryLeadDays);
            _settings = settings;
        }

        var localNow = TimeZoneInfo.ConvertTime(time.GetUtcNow(), settings.TimeZone);
        return new HouseholdToday(DateOnly.FromDateTime(localNow.DateTime), settings.ExpiryLeadDays, settings.TimeZoneId);
    }
}
