using System.Net;
using FoodAssistant.Server.Tests.Auth;

namespace FoodAssistant.Server.Tests;

/// <summary>The server needs its database to start (migrations run at startup), hence the container.</summary>
public sealed class HealthEndpointTests(PostgresContainerFixture postgres)
{
    [Fact]
    public async Task Health_ReturnsOk()
    {
        await using var app = new ServerTestApp(postgres.NewDatabaseConnectionString());
        using var client = app.CreateClient();

        using var response = await client.GetAsync(new Uri("/health", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
