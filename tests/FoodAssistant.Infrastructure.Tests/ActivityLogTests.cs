using FoodAssistant.Application.Activity;
using FoodAssistant.Application.Households;
using FoodAssistant.Infrastructure.Activity;
using Microsoft.EntityFrameworkCore;

namespace FoodAssistant.Infrastructure.Tests;

/// <summary>Feed entries are stored with the caller's next save, in the active household.</summary>
public sealed class ActivityLogTests(PostgresFixture postgres)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Add_IsStoredWithTheNextSave_InTheActiveHousehold()
    {
        var time = new ManualTimeProvider();
        var now = new DateTimeOffset(2026, 11, 18, 10, 0, 0, TimeSpan.Zero);
        time.Set(now);
        await using var database = await TestDatabase.CreateAsync(postgres.NewDatabaseConnectionString(), time: time);
        var (householdId, userId) = await SeedAsync(database);
        await using var scope = database.CreateScope(activeHousehold: householdId);
        var subject = Guid.CreateVersion7();

        var id = scope.Get<IActivityLog>().Add(
            new ActivityEntry(ActivityArea.Inventory, "batch_opened", subject, null, userId, "{\"state\":\"sealed\"}", 4));
        Assert.Empty(await scope.Db.Set<HouseholdActivity>().AsNoTracking().ToListAsync(Ct));
        await scope.Db.SaveChangesAsync(Ct);

        var stored = await scope.Db.Set<HouseholdActivity>().AsNoTracking().SingleAsync(Ct);
        Assert.Equal(id, stored.Id);
        Assert.Equal(householdId, stored.HouseholdId);
        Assert.Equal((ActivityArea.Inventory, "batch_opened", subject, userId, 4L), (stored.Area, stored.Kind, stored.SubjectId, stored.UserId, stored.SubjectVersionAfter!.Value));
        Assert.Equal(now, stored.OccurredAt);
    }

    [Fact]
    public async Task Add_BySystem_StoresNoPerson()
    {
        await using var database = await TestDatabase.CreateAsync(postgres.NewDatabaseConnectionString());
        var (householdId, _) = await SeedAsync(database);
        await using var scope = database.CreateScope(activeHousehold: householdId);

        scope.Get<IActivityLog>().Add(
            new ActivityEntry(ActivityArea.Shopping, "item_added", Guid.CreateVersion7(), null, HouseholdScopes.SystemUserId, null, null));
        await scope.Db.SaveChangesAsync(Ct);

        Assert.Null((await scope.Db.Set<HouseholdActivity>().AsNoTracking().SingleAsync(Ct)).UserId);
    }

    [Fact]
    public async Task Add_WithoutActiveHousehold_Throws()
    {
        await using var database = await TestDatabase.CreateAsync(postgres.NewDatabaseConnectionString(), migrate: false);
        await using var scope = database.CreateScope();

        Assert.Throws<InvalidOperationException>(() => scope.Get<IActivityLog>().Add(
            new ActivityEntry(ActivityArea.Catalog, "product_created", Guid.CreateVersion7(), null, null, null, null)));
    }

    private static async Task<(Guid HouseholdId, Guid UserId)> SeedAsync(TestDatabase database)
    {
        await using var scope = database.CreateScope();
        var user = await Seed.UserAsync(scope.Db, "owner");
        var household = await Seed.HouseholdAsync(scope.Db, user.Id);
        return (household.Id, user.Id);
    }
}
