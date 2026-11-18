using FoodAssistant.Application.Stores;
using FoodAssistant.Domain.Shopping;

namespace FoodAssistant.Infrastructure.Tests.Stores;

/// <summary>Store persistence against PostgreSQL: transition counters and the manual order.</summary>
public sealed class StoreSectionOrderStoreTests(PostgresFixture postgres)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task RecordTransition_UpsertsTheCounter_AndRemembersTheLastCheck()
    {
        await using var database = await TestDatabase.CreateAsync(postgres.NewDatabaseConnectionString());
        var (householdId, storeId) = await SeedStoreAsync(database);
        var at = new DateTimeOffset(2026, 11, 18, 10, 0, 0, TimeSpan.Zero);

        foreach (var (from, to) in new (string?, string)[]
                 {
                     (null, StoreSections.Produce),
                     (StoreSections.Produce, StoreSections.DairyChilled),
                     (StoreSections.Produce, StoreSections.DairyChilled),
                 })
        {
            await using var scope = database.CreateScope(householdId);
            var orders = scope.Get<IStoreSectionOrderStore>();
            var last = (await orders.GetLastCheckAsync(storeId, Ct))!.Value;
            Assert.True(await orders.RecordTransitionAsync(storeId, last, from, to, at, Ct));
        }

        await using var check = database.CreateScope(householdId);
        var store = check.Get<IStoreSectionOrderStore>();
        var transition = Assert.Single(await store.GetTransitionsAsync(storeId, Ct));
        Assert.Equal(new SectionTransition(StoreSections.Produce, StoreSections.DairyChilled, 2), transition);
        Assert.Equal<(string?, DateTimeOffset?)?>((StoreSections.DairyChilled, at), await store.GetLastCheckAsync(storeId, Ct));
    }

    [Fact]
    public async Task RecordTransition_LastCheckChangedMeanwhile_ReturnsFalseAndSavesNothing()
    {
        await using var database = await TestDatabase.CreateAsync(postgres.NewDatabaseConnectionString());
        var (householdId, storeId) = await SeedStoreAsync(database);
        var at = new DateTimeOffset(2026, 11, 18, 10, 0, 0, TimeSpan.Zero);
        await using var slow = database.CreateScope(householdId);
        await using var fast = database.CreateScope(householdId);
        var slowStore = slow.Get<IStoreSectionOrderStore>();
        var staleLast = (await slowStore.GetLastCheckAsync(storeId, Ct))!.Value;

        // Another check-off lands between reading the last check-off and recording this one.
        Assert.True(await fast.Get<IStoreSectionOrderStore>().RecordTransitionAsync(storeId, staleLast, null, StoreSections.Bakery, at, Ct));
        var recorded = await slowStore.RecordTransitionAsync(
            storeId, staleLast, StoreSections.Produce, StoreSections.DairyChilled, at.AddMinutes(1), Ct);

        Assert.False(recorded);
        await using var check = database.CreateScope(householdId);
        var store = check.Get<IStoreSectionOrderStore>();
        Assert.Empty(await store.GetTransitionsAsync(storeId, Ct));
        Assert.Equal<(string?, DateTimeOffset?)?>((StoreSections.Bakery, at), await store.GetLastCheckAsync(storeId, Ct));
    }

    [Fact]
    public async Task ManualOrder_IsReplacedAndCleared_PerStoreAndHousehold()
    {
        await using var database = await TestDatabase.CreateAsync(postgres.NewDatabaseConnectionString());
        var (householdId, storeId) = await SeedStoreAsync(database);
        var (otherHouseholdId, _) = await SeedStoreAsync(database, "other");

        await using (var scope = database.CreateScope(householdId))
        {
            var store = scope.Get<IStoreSectionOrderStore>();
            await store.SetManualOrderAsync(storeId, [StoreSections.Frozen, StoreSections.Produce], Ct);
            await store.SetManualOrderAsync(storeId, [StoreSections.Produce, StoreSections.Bakery], Ct);
            var manual = await store.GetManualOrderAsync(storeId, Ct);
            Assert.Equal([(StoreSections.Produce, 0), (StoreSections.Bakery, 1)], manual.OrderBy(p => p.Value).Select(p => (p.Key, p.Value)));
        }

        await using (var foreign = database.CreateScope(otherHouseholdId))
        {
            Assert.Empty(await foreign.Get<IStoreSectionOrderStore>().GetManualOrderAsync(storeId, Ct));
            Assert.Null(await foreign.Get<IStoreSectionOrderStore>().GetLastCheckAsync(storeId, Ct));
        }

        await using var clear = database.CreateScope(householdId);
        await clear.Get<IStoreSectionOrderStore>().ClearManualOrderAsync(storeId, Ct);
        Assert.Empty(await clear.Get<IStoreSectionOrderStore>().GetManualOrderAsync(storeId, Ct));
    }

    private static async Task<(Guid HouseholdId, Guid StoreId)> SeedStoreAsync(TestDatabase database, string owner = "owner")
    {
        await using var seed = database.CreateScope();
        var user = await Seed.UserAsync(seed.Db, owner);
        var householdId = (await Seed.HouseholdAsync(seed.Db, user.Id)).Id;
        seed.Household.Activate(householdId);
        return (householdId, (await M1Seed.StoreAsync(seed.Db, householdId)).Id);
    }
}
