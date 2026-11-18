using System.Globalization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace FoodAssistant.Server.Tests.Auth;

/// <summary>
/// The real server against its own PostgreSQL database; startup runs the migrations. Clients talk
/// HTTPS so the <c>Secure</c> session cookie is kept, and do not follow redirects (OIDC tests).
/// </summary>
public sealed class ServerTestApp(string connectionString, int authStrictPermitLimit = 10_000) : WebApplicationFactory<Program>
{
    public MutableTimeProvider Time { get; } = new();

    public TestClient CreateTestClient() =>
        new(CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
            HandleCookies = true,
        }));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("ConnectionStrings:FoodAssistant", connectionString);

        // All test requests come from one address; the sign-in limit is covered in AuthRateLimitTests.
        builder.UseSetting("FoodAssistant:RateLimits:AuthStrictPermitLimit", authStrictPermitLimit.ToString(CultureInfo.InvariantCulture));
        builder.ConfigureTestServices(services => services.AddSingleton<TimeProvider>(Time));
    }
}
