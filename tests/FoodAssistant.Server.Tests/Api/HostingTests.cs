using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace FoodAssistant.Server.Tests.Api;

public sealed class HostingTests(ApiTestFactory factory) : IClassFixture<ApiTestFactory>
{
    [Fact]
    public void Production_WithoutPublicUrl_RefusesToStart()
    {
        using var production = factory.WithWebHostBuilder(builder => builder
            .UseEnvironment("Production")
            .UseSetting("FoodAssistant:DataProtectionPath", Path.Combine(Path.GetTempPath(), "foodassistant-tests", Guid.NewGuid().ToString("N"))));

        // The message is checked in HostingOptionsValidationTests: a failing factory start sometimes
        // surfaces as ObjectDisposedException instead of the validation error. Here: it does not start.
        Assert.ThrowsAny<Exception>(() => production.CreateClient());
    }

    [Fact]
    public async Task Production_WithPublicUrl_Starts()
    {
        using var production = factory.WithWebHostBuilder(builder => builder
            .UseEnvironment("Production")
            .UseSetting("FoodAssistant:PublicUrl", "https://food.example.com")
            .UseSetting("FoodAssistant:DataProtectionPath", Path.Combine(Path.GetTempPath(), "foodassistant-tests", Guid.NewGuid().ToString("N"))));
        using var client = production.CreateClient();

        using var response = await client.GetAsync(new Uri("/api/v1/info", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("172.18.0.5", "203.0.113.7", "https")]
    [InlineData("127.0.0.1", "203.0.113.7", "https")]
    [InlineData("198.51.100.1", "198.51.100.1", "http")]
    public async Task ForwardedHeaders_AreOnlyTrustedFromConfiguredProxies(string proxy, string expectedIp, string expectedScheme)
    {
        using var configured = factory.WithWebHostBuilder(builder => builder.UseSetting("FoodAssistant:TrustedProxies", "172.16.0.1/12"));
        configured.CreateClient().Dispose();

        var context = await configured.Server.SendAsync(
            c =>
            {
                c.Request.Method = HttpMethods.Get;
                c.Request.Path = $"{TestEndpoints.Root}/client";
                c.Connection.RemoteIpAddress = IPAddress.Parse(proxy);
                c.Request.Headers["X-Forwarded-For"] = "203.0.113.7";
                c.Request.Headers["X-Forwarded-Proto"] = "https";
            },
            TestContext.Current.CancellationToken);

        using var body = await JsonDocument.ParseAsync(context.Response.Body, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(expectedIp, body.RootElement.GetProperty("ip").GetString());
        Assert.Equal(expectedScheme, body.RootElement.GetProperty("scheme").GetString());
    }
}
