using FoodAssistant.Application.Households;
using Microsoft.EntityFrameworkCore;

namespace FoodAssistant.Infrastructure.Tests;

/// <summary>"Today" follows the household's time zone, not the server's (AGENTS.md 2.4).</summary>
public sealed class HouseholdCalendarTests(PostgresFixture postgres)
{
    // 23:30 UTC is already the next day in Berlin (UTC+1 in November).
    private static readonly DateTimeOffset LateEveningUtc = new(2026, 11, 18, 23, 30, 0, TimeSpan.Zero);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Today_IsTheDateInTheHouseholdTimeZone()
    {
        var time = new ManualTimeProvider();
        time.Set(LateEveningUtc);
        await using var database = await TestDatabase.CreateAsync(postgres.NewDatabaseConnectionString(), time: time);
        var (berlin, utc) = await SeedAsync(database);

        await using (var scope = database.CreateScope(activeHousehold: berlin))
        {
            var today = await scope.Get<IHouseholdCalendar>().GetTodayAsync(Ct);
            Assert.Equal(new HouseholdToday(new DateOnly(2026, 11, 19), 2, "Europe/Berlin"), today);
        }

        await using (var scope = database.CreateScope(activeHousehold: utc))
        {
            var today = await scope.Get<IHouseholdCalendar>().GetTodayAsync(Ct);
            Assert.Equal(new HouseholdToday(new DateOnly(2026, 11, 18), 5, "Etc/UTC"), today);
        }
    }

    [Fact]
    public async Task Today_ChangesAtLocalMidnight_WithinOneScope()
    {
        var time = new ManualTimeProvider();
        time.Set(new DateTimeOffset(2026, 11, 18, 22, 59, 0, TimeSpan.Zero)); // 23:59 in Berlin
        await using var database = await TestDatabase.CreateAsync(postgres.NewDatabaseConnectionString(), time: time);
        var (berlin, _) = await SeedAsync(database);
        await using var scope = database.CreateScope(activeHousehold: berlin);
        var calendar = scope.Get<IHouseholdCalendar>();

        var before = await calendar.GetTodayAsync(Ct);
        time.Advance(TimeSpan.FromMinutes(2));
        var after = await calendar.GetTodayAsync(Ct);

        Assert.Equal(new DateOnly(2026, 11, 18), before.Today);
        Assert.Equal(new DateOnly(2026, 11, 19), after.Today);
    }

    [Fact]
    public async Task WithoutActiveHousehold_Throws()
    {
        await using var database = await TestDatabase.CreateAsync(postgres.NewDatabaseConnectionString());
        await using var scope = database.CreateScope();

        await Assert.ThrowsAsync<InvalidOperationException>(() => scope.Get<IHouseholdCalendar>().GetTodayAsync(Ct));
    }

    private static async Task<(Guid Berlin, Guid Utc)> SeedAsync(TestDatabase database)
    {
        await using var scope = database.CreateScope();
        var user = await Seed.UserAsync(scope.Db, "owner");
        var berlin = await Seed.HouseholdAsync(scope.Db, user.Id, "Berlin");
        var utc = await Seed.HouseholdAsync(scope.Db, user.Id, "UTC");
        utc.TimeZoneId = "Etc/UTC";
        utc.ExpiryLeadDays = 5;
        await scope.Db.SaveChangesAsync(Ct);
        return (berlin.Id, utc.Id);
    }
}
