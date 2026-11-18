using System.Net;
using FoodAssistant.Contracts.Common;
using FoodAssistant.Server.Api.RateLimiting;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace FoodAssistant.Server.Tests.Api;

public sealed class RateLimitingTests(ApiTestFactory factory) : IClassFixture<ApiTestFactory>
{
    [Fact]
    public void PolicyNames_AreStable()
    {
        Assert.Equal("auth-strict", RateLimitPolicies.AuthStrict);
        Assert.Equal("api", RateLimitPolicies.Api);
    }

    [Fact]
    public async Task AuthStrict_RejectsSixthRequestPerMinuteWith429Problem()
    {
        using var client = factory.CreateClient();
        var uri = new Uri($"{TestEndpoints.Root}/strict", UriKind.Relative);

        for (var i = 0; i < 5; i++)
        {
            using var allowed = await client.GetAsync(uri, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        }

        using var rejected = await client.GetAsync(uri, TestContext.Current.CancellationToken);

        await ProblemAssert.IsProblemAsync(rejected, HttpStatusCode.TooManyRequests, ErrorCodes.RateLimited);
        Assert.NotNull(rejected.Headers.RetryAfter?.Delta);
    }

    [Fact]
    public async Task AuthStrict_GroupsIpv6AddressesByPrefix()
    {
        using var isolated = factory.WithWebHostBuilder(_ => { });
        isolated.CreateClient().Dispose();

        var statuses = new List<int>();
        for (var i = 1; i <= 6; i++)
        {
            var address = IPAddress.Parse($"2001:db8:1:2::{i}");
            var context = await isolated.Server.SendAsync(
                c =>
                {
                    c.Request.Method = HttpMethods.Get;
                    c.Request.Path = $"{TestEndpoints.Root}/strict";
                    c.Connection.RemoteIpAddress = address;
                },
                TestContext.Current.CancellationToken);
            statuses.Add(context.Response.StatusCode);
        }

        Assert.Equal([200, 200, 200, 200, 200, 429], statuses);
    }

    [Fact]
    public async Task UnmatchedApiRequests_AreLimitedToo()
    {
        using var configured = factory.WithWebHostBuilder(builder => builder.UseSetting("FoodAssistant:RateLimits:ApiPermitLimit", "2"));
        using var client = configured.CreateClient();
        var uri = new Uri("/api/v1/does-not-exist", UriKind.Relative);

        using var first = await client.GetAsync(uri, TestContext.Current.CancellationToken);
        using var second = await client.GetAsync(uri, TestContext.Current.CancellationToken);
        using var third = await client.GetAsync(uri, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, second.StatusCode);
        await ProblemAssert.IsProblemAsync(third, HttpStatusCode.TooManyRequests, ErrorCodes.RateLimited);
    }

    [Fact]
    public async Task ApiPolicy_AppliesToTheWholeApiV1Group()
    {
        using var configured = factory.WithWebHostBuilder(builder => builder.UseSetting("FoodAssistant:RateLimits:ApiPermitLimit", "2"));
        using var client = configured.CreateClient();
        var uri = new Uri("/api/v1/info", UriKind.Relative);

        using var first = await client.GetAsync(uri, TestContext.Current.CancellationToken);
        using var second = await client.GetAsync(uri, TestContext.Current.CancellationToken);
        using var third = await client.GetAsync(uri, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        await ProblemAssert.IsProblemAsync(third, HttpStatusCode.TooManyRequests, ErrorCodes.RateLimited);
    }
}
