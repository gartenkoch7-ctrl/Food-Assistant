using System.Net;
using FoodAssistant.Contracts.Accounts;
using FoodAssistant.Contracts.Households;

namespace FoodAssistant.Server.Tests.Auth;

public sealed class SetupTests(PostgresContainerFixture postgres)
{
    [Fact]
    public async Task Setup_CreatesAdminHouseholdAndOwnership_ThenIsClosed()
    {
        await using var app = new ServerTestApp(postgres.NewDatabaseConnectionString());
        using var anonymous = app.CreateTestClient();

        Assert.False((await anonymous.GetJsonAsync<SetupStatusResponse>("/api/v1/setup")).IsSetupComplete);

        var (admin, setup) = await Scenario.SetupAsync(app);
        using (admin)
        {
            Assert.True((await anonymous.GetJsonAsync<SetupStatusResponse>("/api/v1/setup")).IsSetupComplete);

            var me = await admin.GetJsonAsync<MeResponse>("/api/v1/me");
            Assert.Equal(setup.UserId, me.UserId);
            Assert.True(me.IsServerAdmin);
            Assert.False(me.MustChangePassword);
            var household = Assert.Single(me.Households);
            Assert.Equal(setup.HouseholdId, household.Id);
            Assert.Equal(HouseholdRole.Owner, household.Role);
            Assert.Equal("Europe/Berlin", household.TimeZone);
        }
    }

    [Fact]
    public async Task Setup_Second_Returns409()
    {
        await using var app = new ServerTestApp(postgres.NewDatabaseConnectionString());
        using var client = app.CreateTestClient();
        using var first = await client.PostAsync("/api/v1/setup", Scenario.SetupRequest());
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        using var second = await client.PostAsync(
            "/api/v1/setup",
            Scenario.SetupRequest() with { UserName = "intruder" });

        await TestClient.AssertProblemAsync(second, HttpStatusCode.Conflict, AccountErrorCodes.SetupAlreadyDone);
    }

    [Theory]
    [InlineData("short", "", "Europe/Berlin", "password")]
    [InlineData("long enough password", "", "Europe/Berlin", "householdName")]
    [InlineData("long enough password", "Home", "W. Europe Standard Time", "timeZone")]
    public async Task Setup_InvalidInput_Returns400WithFieldErrors(
        string password,
        string householdName,
        string timeZone,
        string field)
    {
        await using var app = new ServerTestApp(postgres.NewDatabaseConnectionString());
        using var client = app.CreateTestClient();

        using var response = await client.PostAsync(
            "/api/v1/setup",
            new SetupRequest("admin", password, "Admin", householdName, timeZone));

        await TestClient.AssertProblemAsync(response, HttpStatusCode.BadRequest, AccountErrorCodes.ValidationFailed);
        Assert.Contains($"\"{field}\"", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), StringComparison.Ordinal);
        Assert.False((await client.GetJsonAsync<SetupStatusResponse>("/api/v1/setup")).IsSetupComplete);
    }
}
