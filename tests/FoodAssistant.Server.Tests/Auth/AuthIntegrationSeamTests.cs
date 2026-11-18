using System.Net;
using System.Net.Http.Json;
using FoodAssistant.Contracts.Common;
using FoodAssistant.Contracts.Households;
using Microsoft.AspNetCore.Hosting;

namespace FoodAssistant.Server.Tests.Auth;

/// <summary>Where the auth endpoints meet the shared API conventions (rate limits, idempotency).</summary>
public sealed class AuthIntegrationSeamTests(PostgresContainerFixture postgres)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task InvalidTokenRequests_AreRateLimitedPerAddress()
    {
        // OpenIddict rejects these inside UseAuthentication; the limiter must run before it.
        await using var app = new ServerTestApp(postgres.NewDatabaseConnectionString());
        using var limited = app.WithWebHostBuilder(b => b.UseSetting("FoodAssistant:RateLimits:ApiPermitLimit", "2"));
        using var client = limited.CreateClient(new() { BaseAddress = new Uri("https://localhost") });

        for (var i = 0; i < 2; i++)
        {
            using var rejected = await PostInvalidGrantAsync(client);
            Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        }

        using var response = await PostInvalidGrantAsync(client);
        await TestClient.AssertProblemAsync(response, HttpStatusCode.TooManyRequests, ErrorCodes.RateLimited);
    }

    [Fact]
    public async Task AntiforgeryRejection_IsNotStored_RetryWithSameKeyRuns()
    {
        await using var app = new ServerTestApp(postgres.NewDatabaseConnectionString());
        var (admin, _) = await Scenario.SetupAsync(app);
        using (admin)
        {
            var key = Guid.NewGuid().ToString();
            var body = new CreateHouseholdRequest("Cabin", "Europe/Vienna");

            admin.Http.DefaultRequestHeaders.Remove("X-XSRF-TOKEN");
            using (var rejected = await PostWithKeyAsync(admin.Http, body, key))
            {
                Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
            }

            await admin.RefreshAntiforgeryAsync();
            using var retried = await PostWithKeyAsync(admin.Http, body, key);
            Assert.Equal(HttpStatusCode.Created, retried.StatusCode);
        }
    }

    private static Task<HttpResponseMessage> PostInvalidGrantAsync(HttpClient client) =>
        client.PostAsync(
            new Uri("/connect/token", UriKind.Relative),
            new FormUrlEncodedContent(new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["grant_type"] = "refresh_token",
                ["client_id"] = "foodassistant-mobile",
                ["refresh_token"] = "not-a-token",
            }),
            Ct);

    private static async Task<HttpResponseMessage> PostWithKeyAsync(HttpClient http, CreateHouseholdRequest body, string key)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri("/api/v1/households", UriKind.Relative))
        {
            Content = JsonContent.Create(body),
        };
        request.Headers.Add("Idempotency-Key", key);
        return await http.SendAsync(request, Ct);
    }
}
