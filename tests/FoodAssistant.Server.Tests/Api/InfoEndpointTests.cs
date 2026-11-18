using System.Net;
using System.Net.Http.Json;
using FoodAssistant.Contracts.Common;
using FoodAssistant.Server.Api.Info;
using FoodAssistant.Server.Composition;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace FoodAssistant.Server.Tests.Api;

public sealed class InfoEndpointTests(ApiTestFactory factory) : IClassFixture<ApiTestFactory>
{
    [Fact]
    public async Task Info_AnonymousReturnsVersionsAndFeatures()
    {
        using var client = factory.CreateClient();

        var info = await client.GetFromJsonAsync<ServerInfoDto>(new Uri("/api/v1/info", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.NotNull(info);
        Assert.False(string.IsNullOrWhiteSpace(info.ServerVersion));
        Assert.Equal("0.0.0", info.MinimumAppVersion);
        Assert.Equal(["v1"], info.ApiVersions);
        Assert.False(info.Features.Ai);
    }

    [Fact]
    public async Task Info_UsesCamelCaseJson()
    {
        using var client = factory.CreateClient();

        var json = await client.GetStringAsync(new Uri("/api/v1/info", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Contains("\"serverVersion\":", json, StringComparison.Ordinal);
        Assert.Contains("\"features\":{\"ai\":false}", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Info_MinimumAppVersionComesFromConfiguration()
    {
        using var configured = factory.WithWebHostBuilder(builder => builder.UseSetting("FoodAssistant:MinimumAppVersion", "1.4.0"));
        using var client = configured.CreateClient();

        var info = await client.GetFromJsonAsync<ServerInfoDto>(new Uri("/api/v1/info", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal("1.4.0", info?.MinimumAppVersion);
    }

    // Checked on the real registration without a host: a failing WebApplicationFactory start sometimes
    // surfaces as ObjectDisposedException instead of the validation error (see HostingOptionsValidationTests).
    [Theory]
    [InlineData("v1.2")]
    [InlineData("1.2")]
    [InlineData("1.2.x")]
    [InlineData("+1.4.0")]
    [InlineData(" 1.4.0")]
    public void InvalidMinimumAppVersion_IsRejected(string value)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection([new KeyValuePair<string, string?>("FoodAssistant:MinimumAppVersion", value)])
            .Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddServerInfo();
        using var provider = services.BuildServiceProvider();

        var exception = Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<ServerInfoOptions>>().Value);

        Assert.Contains("MinimumAppVersion", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void InvalidMinimumAppVersion_RefusesToStart()
    {
        using var configured = factory.WithWebHostBuilder(builder => builder.UseSetting("FoodAssistant:MinimumAppVersion", "1.2"));

        Assert.ThrowsAny<Exception>(() => configured.CreateClient());
    }

    [Fact]
    public async Task Info_IsNotIdempotencyOrValidationSensitive()
    {
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri("/api/v1/info", UriKind.Relative));
        request.Headers.Add("Idempotency-Key", "not-a-uuid");

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
