using System.Net;
using FoodAssistant.Contracts.Accounts;

namespace FoodAssistant.Server.Tests.Auth;

public sealed class LoginTests(PostgresContainerFixture postgres)
{
    [Fact]
    public async Task Login_SetsHardenedSessionCookie_AndLogoutEndsSession()
    {
        await using var app = new ServerTestApp(postgres.NewDatabaseConnectionString());
        using var setupClient = app.CreateTestClient();
        using (await setupClient.PostAsync("/api/v1/setup", Scenario.SetupRequest()))
        {
        }

        using var client = app.CreateTestClient();
        await client.RefreshAntiforgeryAsync();
        using var login = await client.PostAsync("/api/v1/auth/login", new LoginRequest(Scenario.AdminUserName, Scenario.Password));

        Assert.Equal(HttpStatusCode.NoContent, login.StatusCode);
        var cookie = Assert.Single(login.Headers.GetValues("Set-Cookie"), c => c.StartsWith("__Host-FoodAssistant.Session=", StringComparison.Ordinal));
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("expires=", cookie, StringComparison.OrdinalIgnoreCase);

        await client.RefreshAntiforgeryAsync();
        using (var me = await client.GetAsync("/api/v1/me"))
        {
            Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        }

        using (var logout = await client.PostAsync("/api/v1/auth/logout", new { }))
        {
            Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        }

        using var afterLogout = await client.GetAsync("/api/v1/me");
        Assert.Equal(HttpStatusCode.Unauthorized, afterLogout.StatusCode);
    }

    [Fact]
    public async Task Login_WithoutAntiforgeryToken_IsRejected()
    {
        await using var app = new ServerTestApp(postgres.NewDatabaseConnectionString());
        var (admin, _) = await Scenario.SetupAsync(app);
        admin.Dispose();
        using var client = app.CreateTestClient();

        using var response = await client.PostAsync("/api/v1/auth/login", new LoginRequest(Scenario.AdminUserName, Scenario.Password));

        await TestClient.AssertProblemAsync(response, HttpStatusCode.BadRequest, AccountErrorCodes.AntiforgeryInvalid);
    }

    [Fact]
    public async Task CookieWrite_WithoutAntiforgeryToken_IsRejected()
    {
        await using var app = new ServerTestApp(postgres.NewDatabaseConnectionString());
        var (admin, _) = await Scenario.SetupAsync(app);
        using (admin)
        {
            admin.Http.DefaultRequestHeaders.Remove("X-XSRF-TOKEN");

            using var response = await admin.PostAsync("/api/v1/auth/logout", new { });

            await TestClient.AssertProblemAsync(response, HttpStatusCode.BadRequest, AccountErrorCodes.AntiforgeryInvalid);
        }
    }

    [Theory]
    [InlineData(Scenario.AdminUserName, "wrong password!")]
    [InlineData("nobody", Scenario.Password)]
    public async Task Login_WrongCredentials_Returns401WithoutHint(string userName, string password)
    {
        await using var app = new ServerTestApp(postgres.NewDatabaseConnectionString());
        var (admin, _) = await Scenario.SetupAsync(app);
        admin.Dispose();
        using var client = app.CreateTestClient();

        using var response = await client.LoginAsync(userName, password);

        await TestClient.AssertProblemAsync(response, HttpStatusCode.Unauthorized, AccountErrorCodes.InvalidCredentials);
    }

    [Fact]
    public async Task Login_FiveFailures_LockTheAccount()
    {
        await using var app = new ServerTestApp(postgres.NewDatabaseConnectionString());
        var (admin, _) = await Scenario.SetupAsync(app);
        admin.Dispose();
        using var client = app.CreateTestClient();

        for (var attempt = 0; attempt < 5; attempt++)
        {
            using var failed = await client.LoginAsync(Scenario.AdminUserName, "wrong password!");
        }

        using var correctButLocked = await client.LoginAsync(Scenario.AdminUserName, Scenario.Password);

        await TestClient.AssertProblemAsync(correctButLocked, HttpStatusCode.TooManyRequests, AccountErrorCodes.LockedOut);
    }

    [Fact]
    public async Task Me_Anonymous_Returns401()
    {
        await using var app = new ServerTestApp(postgres.NewDatabaseConnectionString());
        using var client = app.CreateTestClient();

        using var response = await client.GetAsync("/api/v1/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
