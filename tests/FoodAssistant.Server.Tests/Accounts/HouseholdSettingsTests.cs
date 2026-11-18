using System.Net;
using System.Net.Http.Json;
using FoodAssistant.Application.Households;
using FoodAssistant.Contracts.Households;
using FoodAssistant.Server.Tests.Auth;
using Microsoft.Extensions.DependencyInjection;

namespace FoodAssistant.Server.Tests.Accounts;

public sealed class HouseholdSettingsTests(PostgresContainerFixture postgres)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static string Url(Guid householdId) => $"/api/v1/households/{householdId}";

    private static async Task<HttpResponseMessage> PutAsync(
        TestClient client, Guid householdId, UpdateHouseholdRequest body, string? ifMatch)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, new Uri(Url(householdId), UriKind.Relative))
        {
            Content = JsonContent.Create(body),
        };
        if (ifMatch is not null)
        {
            request.Headers.TryAddWithoutValidation("If-Match", ifMatch);
        }

        return await client.Http.SendAsync(request, Ct);
    }

    [Fact]
    public async Task AllRoles_CanRead_WithETag()
    {
        await using var app = new ServerTestApp(postgres.NewDatabaseConnectionString());
        var (admin, setup) = await Scenario.SetupAsync(app);
        using var _ = admin;
        var (restricted, _) = await Scenario.AddMemberAsync(app, admin, setup.HouseholdId, HouseholdRole.Restricted, "kid");
        using var __ = restricted;

        using var response = await restricted.GetAsync(Url(setup.HouseholdId));
        var settings = await TestClient.ReadAsync<HouseholdSettingsDto>(response);

        Assert.Equal(("Home", "Europe/Berlin", 2), (settings.Name, settings.TimeZone, settings.ExpiryLeadDays));
        Assert.Equal($"\"{settings.Version}\"", response.Headers.ETag?.Tag);
    }

    [Fact]
    public async Task Owner_Changes_AndNewLeadTimeReachesTheCalendar()
    {
        await using var app = new ServerTestApp(postgres.NewDatabaseConnectionString());
        var (admin, setup) = await Scenario.SetupAsync(app);
        using var _ = admin;
        var current = await admin.GetJsonAsync<HouseholdSettingsDto>(Url(setup.HouseholdId));

        using var response = await PutAsync(
            admin, setup.HouseholdId, new UpdateHouseholdRequest("  Flat  ", "Europe/Vienna", 5), $"\"{current.Version}\"");
        var updated = await TestClient.ReadAsync<HouseholdSettingsDto>(response);

        Assert.Equal(("Flat", "Europe/Vienna", 5), (updated.Name, updated.TimeZone, updated.ExpiryLeadDays));
        Assert.NotEqual(current.Version, updated.Version);
        Assert.Equal($"\"{updated.Version}\"", response.Headers.ETag?.Tag);

        await using var scope = app.Services.GetRequiredService<IHouseholdScopeFactory>().CreateScope(setup.HouseholdId);
        var today = await scope.ServiceProvider.GetRequiredService<IHouseholdCalendar>().GetTodayAsync(Ct);
        Assert.Equal((5, "Europe/Vienna"), (today.ExpiryLeadDays, today.TimeZoneId));
    }

    [Fact]
    public async Task Member_CannotChange_Returns403()
    {
        await using var app = new ServerTestApp(postgres.NewDatabaseConnectionString());
        var (admin, setup) = await Scenario.SetupAsync(app);
        using var _ = admin;
        var (member, _) = await Scenario.AddMemberAsync(app, admin, setup.HouseholdId, HouseholdRole.Member, "member");
        using var __ = member;
        var current = await member.GetJsonAsync<HouseholdSettingsDto>(Url(setup.HouseholdId));

        using var response = await PutAsync(
            member, setup.HouseholdId, new UpdateHouseholdRequest("X", "Europe/Berlin", 2), $"\"{current.Version}\"");

        await TestClient.AssertProblemAsync(response, HttpStatusCode.Forbidden, HouseholdErrorCodes.Forbidden);
    }

    [Theory]
    [InlineData("", "Europe/Berlin", 2, "name")]
    [InlineData("Home", "W. Europe Standard Time", 2, "timeZone")]
    [InlineData("Home", "Europe/Berlin", 15, "expiryLeadDays")]
    [InlineData("Home", "Europe/Berlin", -1, "expiryLeadDays")]
    public async Task InvalidInput_Returns400WithFieldError(string name, string timeZone, int leadDays, string field)
    {
        await using var app = new ServerTestApp(postgres.NewDatabaseConnectionString());
        var (admin, setup) = await Scenario.SetupAsync(app);
        using var _ = admin;
        var current = await admin.GetJsonAsync<HouseholdSettingsDto>(Url(setup.HouseholdId));

        using var response = await PutAsync(
            admin, setup.HouseholdId, new UpdateHouseholdRequest(name, timeZone, leadDays), $"\"{current.Version}\"");

        await TestClient.AssertProblemAsync(response, HttpStatusCode.BadRequest, HouseholdErrorCodes.ValidationFailed);
        Assert.Contains($"\"{field}\"", await response.Content.ReadAsStringAsync(Ct), StringComparison.Ordinal);
    }

    [Fact]
    public async Task StaleOrMissingIfMatch_Returns412Or428()
    {
        await using var app = new ServerTestApp(postgres.NewDatabaseConnectionString());
        var (admin, setup) = await Scenario.SetupAsync(app);
        using var _ = admin;
        var current = await admin.GetJsonAsync<HouseholdSettingsDto>(Url(setup.HouseholdId));
        var body = new UpdateHouseholdRequest("Flat", "Europe/Berlin", 3);
        using (var first = await PutAsync(admin, setup.HouseholdId, body, $"\"{current.Version}\""))
        {
            Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        }

        using var stale = await PutAsync(admin, setup.HouseholdId, body with { Name = "Other" }, $"\"{current.Version}\"");
        using var missing = await PutAsync(admin, setup.HouseholdId, body, ifMatch: null);

        await TestClient.AssertProblemAsync(stale, HttpStatusCode.PreconditionFailed, "common.precondition_failed");
        await TestClient.AssertProblemAsync(missing, HttpStatusCode.PreconditionRequired, "common.precondition_required");
        var stored = await admin.GetJsonAsync<HouseholdSettingsDto>(Url(setup.HouseholdId));
        Assert.Equal("Flat", stored.Name);
    }

    [Fact]
    public async Task ForeignHousehold_Returns404()
    {
        await using var app = new ServerTestApp(postgres.NewDatabaseConnectionString());
        var (admin, setup) = await Scenario.SetupAsync(app);
        using var _ = admin;
        var foreign = await Scenario.CreateHouseholdAsync(admin, "Neighbours");
        var (outsider, _) = await Scenario.AddMemberAsync(app, admin, foreign, HouseholdRole.Owner, "outsider");
        using var __ = outsider;

        using var read = await outsider.GetAsync(Url(setup.HouseholdId));
        using var write = await PutAsync(
            outsider, setup.HouseholdId, new UpdateHouseholdRequest("Mine now", "Europe/Berlin", 2), "*");

        await TestClient.AssertProblemAsync(read, HttpStatusCode.NotFound, HouseholdErrorCodes.NotFound);
        await TestClient.AssertProblemAsync(write, HttpStatusCode.NotFound, HouseholdErrorCodes.NotFound);
    }
}
