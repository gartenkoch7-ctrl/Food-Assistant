using FoodAssistant.Application.Households;
using FoodAssistant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FoodAssistant.Infrastructure.Tests;

/// <summary>Household separation through the EF Core query filters (ADR-0002).</summary>
public sealed class HouseholdQueryFilterTests(PostgresFixture postgres)
{
    [Fact]
    public async Task HouseholdData_OnlyActiveHouseholdIsVisible()
    {
        await using var database = await TestDatabase.CreateAsync(postgres.NewDatabaseConnectionString());
        var (a, b) = await SeedTwoHouseholdsAsync(database);
        var ct = TestContext.Current.CancellationToken;

        await using (var scope = database.CreateScope(activeHousehold: a))
        {
            var invitations = await scope.Db.Invitations.ToListAsync(ct);
            Assert.Equal([a], invitations.Select(i => i.HouseholdId));
        }

        await using (var scope = database.CreateScope(activeHousehold: b))
        {
            var invitations = await scope.Db.Invitations.ToListAsync(ct);
            Assert.Equal([b], invitations.Select(i => i.HouseholdId));
        }
    }

    [Fact]
    public async Task HouseholdData_WithoutActiveHousehold_NothingIsVisible()
    {
        await using var database = await TestDatabase.CreateAsync(postgres.NewDatabaseConnectionString());
        await SeedTwoHouseholdsAsync(database);
        await using var scope = database.CreateScope();

        Assert.Empty(await scope.Db.Invitations.ToListAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task HouseholdData_FilterSwitchesWithTheContextWithinOneDbContext()
    {
        await using var database = await TestDatabase.CreateAsync(postgres.NewDatabaseConnectionString());
        var (a, b) = await SeedTwoHouseholdsAsync(database);
        await using var scope = database.CreateScope(activeHousehold: a);
        var ct = TestContext.Current.CancellationToken;

        var first = await scope.Db.Invitations.AsNoTracking().SingleAsync(ct);
        scope.Household.Activate(b);
        var second = await scope.Db.Invitations.AsNoTracking().SingleAsync(ct);

        Assert.Equal(a, first.HouseholdId);
        Assert.Equal(b, second.HouseholdId);
    }

    [Fact]
    public async Task DeletedHousehold_IsHiddenWithItsMembershipsAndInvitations()
    {
        await using var database = await TestDatabase.CreateAsync(postgres.NewDatabaseConnectionString());
        var (a, b) = await SeedTwoHouseholdsAsync(database);
        var ct = TestContext.Current.CancellationToken;

        await using (var scope = database.CreateScope())
        {
            var household = await scope.Db.Households.SingleAsync(h => h.Id == a, ct);
            household.DeletedAt = DateTimeOffset.UtcNow;
            await scope.Db.SaveChangesAsync(ct);
        }

        await using (var scope = database.CreateScope())
        {
            Assert.Equal([b], await scope.Db.Households.Select(h => h.Id).ToListAsync(ct));
            Assert.Equal([b], await scope.Db.HouseholdMemberships.Select(m => m.HouseholdId).ToListAsync(ct));
            var acrossHouseholds = await scope.Db.Invitations
                .IgnoreQueryFilters([AppDbContext.ActiveHouseholdFilter])
                .Select(i => i.HouseholdId)
                .ToListAsync(ct);
            Assert.Equal([b], acrossHouseholds);
        }
    }

    [Fact]
    public async Task InvitationStore_FindsByTokenAcrossHouseholds_ButListsOnlyActive()
    {
        await using var database = await TestDatabase.CreateAsync(postgres.NewDatabaseConnectionString());
        var (a, b) = await SeedTwoHouseholdsAsync(database);
        await using var scope = database.CreateScope(activeHousehold: a);
        var store = scope.Get<IInvitationStore>();
        var ct = TestContext.Current.CancellationToken;

        var foreign = await store.FindByTokenHashAsync(HashOf(b), ct);
        var listed = await store.ListAsync(ct);
        var revokeForeign = await store.RevokeAsync(foreign!.Id, DateTimeOffset.UtcNow, ct);

        Assert.Equal(b, foreign.HouseholdId);
        Assert.Equal([a], listed.Select(i => i.HouseholdId));
        Assert.Equal(RevokeInvitationOutcome.NotFound, revokeForeign);
    }

    private static string HashOf(Guid householdId) => householdId.ToString("N") + householdId.ToString("N");

    private static async Task<(Guid A, Guid B)> SeedTwoHouseholdsAsync(TestDatabase database)
    {
        await using var scope = database.CreateScope();
        var user = await Seed.UserAsync(scope.Db, "owner");
        var a = await Seed.HouseholdAsync(scope.Db, user.Id, "A");
        var b = await Seed.HouseholdAsync(scope.Db, user.Id, "B");

        // Writes are not filtered: the household id is set explicitly.
        await Seed.InvitationAsync(scope.Db, a.Id, HashOf(a.Id));
        await Seed.InvitationAsync(scope.Db, b.Id, HashOf(b.Id));
        return (a.Id, b.Id);
    }
}
