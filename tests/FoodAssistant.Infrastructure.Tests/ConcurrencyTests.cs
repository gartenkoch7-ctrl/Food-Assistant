using FoodAssistant.Application.Accounts;
using FoodAssistant.Application.Households;
using FoodAssistant.Contracts.Households;
using Microsoft.EntityFrameworkCore;

namespace FoodAssistant.Infrastructure.Tests;

/// <summary>Optimistic concurrency (xmin) and the serialized check-then-act rules.</summary>
public sealed class ConcurrencyTests(PostgresFixture postgres)
{
    private const string Password = "correct horse battery";

    [Fact]
    public async Task Household_ConcurrentUpdate_SecondSaveThrows()
    {
        await using var database = await TestDatabase.CreateAsync(postgres.NewDatabaseConnectionString());
        var ct = TestContext.Current.CancellationToken;
        Guid householdId;
        await using (var seed = database.CreateScope())
        {
            var user = await Seed.UserAsync(seed.Db, "owner");
            householdId = (await Seed.HouseholdAsync(seed.Db, user.Id)).Id;
        }

        await using var first = database.CreateScope();
        await using var second = database.CreateScope();
        var one = await first.Db.Households.SingleAsync(h => h.Id == householdId, ct);
        var two = await second.Db.Households.SingleAsync(h => h.Id == householdId, ct);

        one.Name = "First";
        await first.Db.SaveChangesAsync(ct);
        two.Name = "Second";

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.Db.SaveChangesAsync(ct));
    }

    [Fact]
    public async Task Setup_RunConcurrently_OnlyOneSucceeds()
    {
        await using var database = await TestDatabase.CreateAsync(postgres.NewDatabaseConnectionString());

        var results = await Task.WhenAll(
            Enumerable.Range(0, 4).Select(async i =>
            {
                await using var scope = database.CreateScope();
                return await scope.Get<IAccountProvisioning>().CompleteSetupAsync(
                    new NewUserAccount($"admin{i}", Password, "Admin"),
                    new NewHousehold("Home", "Europe/Berlin"),
                    TestContext.Current.CancellationToken);
            }));

        Assert.Single(results, r => r.Status == ProvisioningStatus.Succeeded);
        Assert.All(
            results.Where(r => r.Status != ProvisioningStatus.Succeeded),
            r => Assert.Equal(ProvisioningStatus.SetupAlreadyDone, r.Status));
    }

    [Fact]
    public async Task Invitation_RedeemedConcurrently_OnlyOneAccountIsCreated()
    {
        await using var database = await TestDatabase.CreateAsync(postgres.NewDatabaseConnectionString());
        Guid invitationId;
        await using (var seed = database.CreateScope())
        {
            var owner = await Seed.UserAsync(seed.Db, "owner");
            var household = await Seed.HouseholdAsync(seed.Db, owner.Id);
            invitationId = (await Seed.InvitationAsync(seed.Db, household.Id, new string('a', 64))).Id;
        }

        var results = await Task.WhenAll(
            Enumerable.Range(0, 4).Select(async i =>
            {
                await using var scope = database.CreateScope();
                return await scope.Get<IAccountProvisioning>().CreateAccountFromInvitationAsync(
                    invitationId,
                    new NewUserAccount($"guest{i}", Password, "Guest"),
                    TestContext.Current.CancellationToken);
            }));

        Assert.Single(results, r => r.Status == ProvisioningStatus.Succeeded);
        await using var check = database.CreateScope();
        Assert.Equal(2, await check.Db.Users.CountAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task LastOwner_TwoOwnersDemoteEachOtherConcurrently_OneOwnerRemains()
    {
        await using var database = await TestDatabase.CreateAsync(postgres.NewDatabaseConnectionString());
        var ct = TestContext.Current.CancellationToken;
        Guid householdId, first, second;
        await using (var seed = database.CreateScope())
        {
            first = (await Seed.UserAsync(seed.Db, "first")).Id;
            second = (await Seed.UserAsync(seed.Db, "second")).Id;
            var household = await Seed.HouseholdAsync(seed.Db, first);
            householdId = household.Id;
            seed.Db.HouseholdMemberships.Add(new Households.HouseholdMembership
            {
                HouseholdId = householdId,
                UserId = second,
                Role = HouseholdRole.Owner,
                JoinedAt = DateTimeOffset.UtcNow,
            });
            await seed.Db.SaveChangesAsync(ct);
        }

        var outcomes = await Task.WhenAll(new[] { first, second }.Select(async userId =>
        {
            await using var scope = database.CreateScope();
            return await scope.Get<IHouseholdStore>().ChangeRoleAsync(householdId, userId, HouseholdRole.Member, ct);
        }));

        Assert.Contains(MembershipChangeOutcome.Succeeded, outcomes);
        Assert.Contains(MembershipChangeOutcome.LastOwner, outcomes);
        await using var check = database.CreateScope();
        Assert.Equal(1, await check.Db.HouseholdMemberships.CountAsync(m => m.Role == HouseholdRole.Owner, ct));
    }
}
