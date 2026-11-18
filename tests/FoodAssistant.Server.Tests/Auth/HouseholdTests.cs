using System.Net;
using FoodAssistant.Contracts.Accounts;
using FoodAssistant.Contracts.Households;

namespace FoodAssistant.Server.Tests.Auth;

public sealed class HouseholdTests(PostgresContainerFixture postgres)
{
    [Fact]
    public async Task ServerAdmin_CreatesHousehold_AndBecomesItsOwner()
    {
        await using var app = new ServerTestApp(postgres.NewDatabaseConnectionString());
        var (admin, _) = await Scenario.SetupAsync(app);
        using var __ = admin;

        var created = await Scenario.CreateHouseholdAsync(admin, "Parents");

        var me = await admin.GetJsonAsync<MeResponse>("/api/v1/me");
        Assert.Equal(2, me.Households.Count);
        Assert.Equal(HouseholdRole.Owner, me.Households.Single(h => h.Id == created).Role);
    }

    [Fact]
    public async Task NonAdmin_CannotCreateHousehold_Returns403()
    {
        await using var app = new ServerTestApp(postgres.NewDatabaseConnectionString());
        var (admin, setup) = await Scenario.SetupAsync(app);
        using var _ = admin;
        var (owner, _) = await Scenario.AddMemberAsync(app, admin, setup.HouseholdId, HouseholdRole.Owner, "owner");
        using var __ = owner;

        using var response = await owner.PostAsync("/api/v1/households", new CreateHouseholdRequest("Mine", "Europe/Berlin"));

        await TestClient.AssertProblemAsync(response, HttpStatusCode.Forbidden, AccountErrorCodes.ServerAdminRequired);
    }

    [Fact]
    public async Task CreateHousehold_InvalidTimeZone_Returns400()
    {
        await using var app = new ServerTestApp(postgres.NewDatabaseConnectionString());
        var (admin, _) = await Scenario.SetupAsync(app);
        using var __ = admin;

        using var response = await admin.PostAsync("/api/v1/households", new CreateHouseholdRequest("Parents", "Mars/Olympus"));

        await TestClient.AssertProblemAsync(response, HttpStatusCode.BadRequest, HouseholdErrorCodes.ValidationFailed);
    }

    [Fact]
    public async Task CreateHousehold_Anonymous_Returns401()
    {
        await using var app = new ServerTestApp(postgres.NewDatabaseConnectionString());
        using var client = app.CreateTestClient();

        using var response = await client.PostAsync("/api/v1/households", new CreateHouseholdRequest("Parents", "Europe/Berlin"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
