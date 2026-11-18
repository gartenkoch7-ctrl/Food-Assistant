using FoodAssistant.Server.Api.Hosting;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace FoodAssistant.Server.Tests.Api;

/// <summary>
/// Server in the Testing environment plus the test-only endpoints of <see cref="TestEndpoints"/>.
/// The endpoints exist only here, never in the product.
/// </summary>
public sealed class ApiTestFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(HostingOptions.TestingEnvironment);
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
