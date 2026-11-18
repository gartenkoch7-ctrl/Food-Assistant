using System.Net;
using System.Net.Http.Headers;
using FoodAssistant.Contracts.Accounts;
using FoodAssistant.Contracts.Households;

namespace FoodAssistant.Server.Tests.Auth;

/// <summary>App sign-in: authorization code + PKCE with the web cookie, then bearer tokens (ADR-0003).</summary>
public sealed class TokenFlowTests(PostgresContainerFixture postgres)
{
    [Fact]
    public async Task CodeWithPkce_IssuesTokens_BearerWorks_RefreshRotates()
    {
        await using var app = new ServerTestApp(postgres.NewDatabaseConnectionString());
        var (admin, setup) = await Scenario.SetupAsync(app);
        using var _ = admin;
        using var phone = app.CreateTestClient();

        var tokens = await OidcFlow.SignInAppAsync(admin, phone);

        phone.Http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.GetProperty("access_token").GetString());
        var me = await phone.GetJsonAsync<MeResponse>("/api/v1/me");
        Assert.Equal(setup.UserId, me.UserId);
        Assert.InRange(tokens.GetProperty("expires_in").GetInt32(), 890, 900);

        // Household policies work with bearer tokens, too.
        var members = await phone.GetJsonAsync<List<HouseholdMemberResponse>>($"/api/v1/households/{setup.HouseholdId}/members");
        Assert.Single(members);

        var oldRefresh = tokens.GetProperty("refresh_token").GetString()!;
        var refreshed = await OidcFlow.TokenAsync(phone, OidcFlow.RefreshForm(oldRefresh));
        Assert.NotEqual(oldRefresh, refreshed.GetProperty("refresh_token").GetString());

        // OpenIddict tolerates a replay for 30 s (lost responses on mobile networks); after that a
        // redeemed refresh token is rejected.
        app.Time.Advance(TimeSpan.FromMinutes(1));
        using var reuse = await OidcFlow.PostFormAsync(phone, "/connect/token", OidcFlow.RefreshForm(oldRefresh));
        Assert.Equal(HttpStatusCode.BadRequest, reuse.StatusCode);
    }

    [Fact]
    public async Task Restart_UpdatesExistingClientRegistration_AndSignInStillWorks()
    {
        var database = postgres.NewDatabaseConnectionString();
        await using (var first = new ServerTestApp(database))
        {
            (await Scenario.SetupAsync(first)).Admin.Dispose();
        }

        await using var second = new ServerTestApp(database);
        using var browser = second.CreateTestClient();
        using (var login = await browser.LoginAsync(Scenario.AdminUserName, Scenario.Password))
        {
            Assert.Equal(HttpStatusCode.NoContent, login.StatusCode);
        }

        using var phone = second.CreateTestClient();
        var tokens = await OidcFlow.SignInAppAsync(browser, phone);
        Assert.Equal(System.Text.Json.JsonValueKind.String, tokens.GetProperty("refresh_token").ValueKind);
    }

    [Fact]
    public async Task CodeWithWrongVerifier_IsRejected()
    {
        await using var app = new ServerTestApp(postgres.NewDatabaseConnectionString());
        var (admin, _) = await Scenario.SetupAsync(app);
        using var _ = admin;
        var code = await OidcFlow.AuthorizeAsync(admin, OidcFlow.NewVerifier());
        using var client = app.CreateTestClient();

        using var response = await OidcFlow.PostFormAsync(client, "/connect/token", OidcFlow.CodeForm(code, OidcFlow.NewVerifier()));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Authorize_WithoutWebSignIn_Returns401WithHint()
    {
        await using var app = new ServerTestApp(postgres.NewDatabaseConnectionString());
        using var client = app.CreateTestClient();

        using var response = await client.GetAsync(OidcFlow.AuthorizeUrl("challenge-for-anonymous-request-1234567890abc"));

        await TestClient.AssertProblemAsync(response, HttpStatusCode.Unauthorized, AccountErrorCodes.LoginRequired);
    }

    [Fact]
    public async Task Api_WithInvalidBearerToken_Returns401()
    {
        await using var app = new ServerTestApp(postgres.NewDatabaseConnectionString());
        using var client = app.CreateTestClient();
        client.Http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "not-a-token");

        using var response = await client.GetAsync("/api/v1/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
