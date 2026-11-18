using FoodAssistant.Application.Households;
using Microsoft.EntityFrameworkCore;

namespace FoodAssistant.Infrastructure.Tests;

public sealed class HouseholdStoreTests(PostgresFixture postgres)
{
    [Fact]
    public async Task ListActiveHouseholdIds_SkipsDeletedHouseholds()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var database = await TestDatabase.CreateAsync(postgres.NewDatabaseConnectionString());
        Guid kept;
        await using (var seed = database.CreateScope())
        {
            var user = await Seed.UserAsync(seed.Db, "owner");
            kept = (await Seed.HouseholdAsync(seed.Db, user.Id, "Kept")).Id;
            var deleted = await Seed.HouseholdAsync(seed.Db, user.Id, "Deleted");
            deleted.DeletedAt = DateTimeOffset.UtcNow;
            await seed.Db.SaveChangesAsync(ct);
        }

        await using var scope = database.CreateScope();
        var ids = await scope.Get<IHouseholdStore>().ListActiveHouseholdIdsAsync(ct);

        Assert.Equal([kept], ids);
        Assert.Equal(2, await scope.Db.Households.IgnoreQueryFilters().CountAsync(ct));
    }
}
