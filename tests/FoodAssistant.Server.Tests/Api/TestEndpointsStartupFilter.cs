using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Routing;

namespace FoodAssistant.Server.Tests.Api;

/// <summary>
/// Adds the test endpoints without touching Program.cs. A startup filter runs in front of the
/// app pipeline: <c>UseRouting</c> here only matches the test endpoints; the app's own routing
/// then skips matching, and the app's endpoint middleware executes them at the end of the real
/// pipeline, so every API middleware applies. They also stay out of the OpenAPI document.
/// </summary>
internal sealed class TestEndpointsStartupFilter : IStartupFilter
{
    // Key under which UseRouting stores its IEndpointRouteBuilder (ASP.NET Core implementation detail).
    private const string EndpointRouteBuilderKey = "__EndpointRouteBuilder";

    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        app.UseRouting();
        var routes = (IEndpointRouteBuilder)app.Properties[EndpointRouteBuilderKey]!;
        TestEndpoints.Map(routes);
        next(app);
    };
}
