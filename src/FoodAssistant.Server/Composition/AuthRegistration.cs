namespace FoodAssistant.Server.Composition;

/// <summary>Identity, OpenIddict and authorization policies (ADR-0002, ADR-0003). Owned by the auth track.</summary>
public static class AuthRegistration
{
    public static WebApplicationBuilder AddAuth(this WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder;
    }

    public static WebApplication UseAuth(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);
        return app;
    }
}
