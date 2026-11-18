using System.Net;
using FoodAssistant.Contracts.Accounts;
using FoodAssistant.Contracts.Common;

namespace FoodAssistant.Server.Tests.Auth;

/// <summary>Sign-in endpoints use the strict per-address limit (ADR-0003), independent of the lockout.</summary>
public sealed class AuthRateLimitTests(PostgresContainerFixture postgres)
{
    [Fact]
    public async Task Login_BeyondStrictLimit_Returns429WithErrorCode()
    {
        // Setup and its login count against the same strict window: 4 = setup + login + 2 attempts.
        await using var app = new ServerTestApp(postgres.NewDatabaseConnectionString(), authStrictPermitLimit: 4);
        await Scenario.SetupAsync(app);

        using var client = app.CreateTestClient();
        await client.RefreshAntiforgeryAsync();
        var request = new LoginRequest(Scenario.AdminUserName, "wrong password");
        using (var first = await client.PostAsync("/api/v1/auth/login", request))
        {
            Assert.Equal(HttpStatusCode.Unauthorized, first.StatusCode);
        }

        using (var second = await client.PostAsync("/api/v1/auth/login", request))
        {
            Assert.Equal(HttpStatusCode.Unauthorized, second.StatusCode);
        }

        using var limited = await client.PostAsync("/api/v1/auth/login", request);
        await TestClient.AssertProblemAsync(limited, HttpStatusCode.TooManyRequests, ErrorCodes.RateLimited);
    }
}
