using FoodAssistant.Server.Api.Hosting;
using FoodAssistant.Server.Tests.Auth;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace FoodAssistant.Server.Tests.Api;

/// <summary>
/// Server in the Testing environment plus the test-only endpoints of <see cref="TestEndpoints"/>.
/// The endpoints exist only here, never in the product. The server migrates its database at
/// startup, so each factory gets its own database in the shared container.
/// </summary>
public sealed class ApiTestFactory(PostgresContainerFixture postgres) : WebApplicationFactory<Program>
{
    private readonly string _connectionString = postgres.NewDatabaseConnectionString();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(HostingOptions.TestingEnvironment);
        builder.UseSetting("ConnectionStrings:FoodAssistant", _connectionString);
        builder.ConfigureServices(services =>
        {
            // The validation source generator registers the validatable types of the compilation
            // that calls AddValidation; this call covers the test endpoints' DTOs.
            services.AddValidation();
            services.AddSingleton<TestState>();
            services.AddTransient<IStartupFilter, TestEndpointsStartupFilter>();
        });
    }
}
