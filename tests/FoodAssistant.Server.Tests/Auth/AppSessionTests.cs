using System.Net;
using System.Text.Json;
using FoodAssistant.Infrastructure.Accounts;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace FoodAssistant.Server.Tests.Auth;

/// <summary>Ending app sessions: revocation, credential changes, lockouts and end-session (ADR-0003).</summary>
public sealed class AppSessionTests(PostgresContainerFixture postgres)
{
    [Fact]
    public async Task RevokedRefreshToken_CannotBeUsed()
    {
        await using var app = new ServerTestApp(postgres.NewDatabaseConnectionString());
        var (admin, _) = await Scenario.SetupAsync(app);
        using var _ = admin;
        using var phone = app.CreateTestClient();
        var refresh = (await OidcFlow.SignInAppAsync(admin, phone)).GetProperty("refresh_token").GetString()!;

        using (var revoke = await OidcFlow.PostFormAsync(phone, "/connect/revoke", new Dictionary<string, string>
               {
                   ["client_id"] = OidcFlow.ClientId,
                   ["token"] = refresh,
                   ["token_type_hint"] = "refresh_token",
               }))
        {
            Assert.Equal(HttpStatusCode.OK, revoke.StatusCode);
        }

        using var reuse = await OidcFlow.PostFormAsync(phone, "/connect/token", OidcFlow.RefreshForm(refresh));
        Assert.Equal(HttpStatusCode.BadRequest, reuse.StatusCode);
    }

    [Fact]
    public async Task ChangedSecurityStamp_EndsAppSessionAtNextRefresh()
    {
        await using var app = new ServerTestApp(postgres.NewDatabaseConnectionString());
        var (admin, setup) = await Scenario.SetupAsync(app);
        using var _ = admin;
        using var phone = app.CreateTestClient();
        var refresh = (await OidcFlow.SignInAppAsync(admin, phone)).GetProperty("refresh_token").GetString()!;

        // What a password change does to the account.
        using (var scope = app.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
            var user = await users.FindByIdAsync(setup.UserId.ToString());
            await users.UpdateSecurityStampAsync(user!);
        }

        using var response = await OidcFlow.PostFormAsync(phone, "/connect/token", OidcFlow.RefreshForm(refresh));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task FailedLoginLockout_DoesNotEndAppSessionOrAdminRight()
    {
        await using var app = new ServerTestApp(postgres.NewDatabaseConnectionString());
        var (admin, _) = await Scenario.SetupAsync(app);
        using var _ = admin;
        using var phone = app.CreateTestClient();
        var refresh = (await OidcFlow.SignInAppAsync(admin, phone)).GetProperty("refresh_token").GetString()!;

        using var attacker = app.CreateTestClient();
        for (var attempt = 0; attempt < 5; attempt++)
        {
            using var failed = await attacker.LoginAsync(Scenario.AdminUserName, "wrong password!");
        }

        var refreshed = await OidcFlow.TokenAsync(phone, OidcFlow.RefreshForm(refresh));
        Assert.Equal(JsonValueKind.String, refreshed.GetProperty("access_token").ValueKind);
        Assert.NotEqual(Guid.Empty, await Scenario.CreateHouseholdAsync(admin, "Still admin"));
    }

    [Fact]
    public async Task EndSession_WithoutIdTokenHint_KeepsBrowserSession()
    {
        await using var app = new ServerTestApp(postgres.NewDatabaseConnectionString());
        var (admin, _) = await Scenario.SetupAsync(app);
        using var _ = admin;

        using (var endSession = await admin.GetAsync("/connect/endsession"))
        {
            Assert.True((int)endSession.StatusCode is >= 300 and < 400, endSession.StatusCode.ToString());
        }

        using var me = await admin.GetAsync("/api/v1/me");
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
    }

    [Fact]
    public async Task EndSession_WithIdTokenHint_EndsBrowserSession()
    {
        await using var app = new ServerTestApp(postgres.NewDatabaseConnectionString());
        var (admin, _) = await Scenario.SetupAsync(app);
        using var _ = admin;
        using var phone = app.CreateTestClient();
        var idToken = (await OidcFlow.SignInAppAsync(admin, phone)).GetProperty("id_token").GetString();

        using (await admin.GetAsync($"/connect/endsession?id_token_hint={Uri.EscapeDataString(idToken!)}"))
        {
        }

        using var me = await admin.GetAsync("/api/v1/me");
        Assert.Equal(HttpStatusCode.Unauthorized, me.StatusCode);
    }
}
