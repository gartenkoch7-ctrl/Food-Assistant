using FoodAssistant.Infrastructure.Sync;
using Microsoft.EntityFrameworkCore;

namespace FoodAssistant.Infrastructure.Tests.Sync;

/// <summary>Households and change-log reads for the sync tests.</summary>
internal static class SyncTestData
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>Two empty households of one owner; the notifier is reset afterwards.</summary>
    public static async Task<(Guid A, Guid B)> TwoHouseholdsAsync(TestDatabase database)
    {
        await using var scope = database.CreateScope();
        var user = await Seed.UserAsync(scope.Db, "owner");
        var a = await Seed.HouseholdAsync(scope.Db, user.Id, "A");
        var b = await Seed.HouseholdAsync(scope.Db, user.Id, "B");
        database.Notifier.Reset();
        return (a.Id, b.Id);
    }

    /// <summary>The change log of one household in sequence order.</summary>
    public static async Task<List<ChangeLogEntry>> ChangeLogAsync(TestDatabase database, Guid householdId)
    {
        await using var scope = database.CreateScope(activeHousehold: householdId);
        return await scope.Db.Set<ChangeLogEntry>().AsNoTracking().OrderBy(e => e.Sequence).ToListAsync(Ct);
    }
}
