namespace FoodAssistant.Server.Api.Hosting;

/// <summary>
/// Puts the forwarded headers middleware in front of the whole pipeline. It must run before
/// anything that reads the client IP or scheme (HSTS, rate limits, auth redirects); the
/// <c>UseApi</c> hook in Program.cs comes too late for that.
/// </summary>
internal sealed class ForwardedHeadersStartupFilter : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        app.UseForwardedHeaders();
        next(app);
    };
}
