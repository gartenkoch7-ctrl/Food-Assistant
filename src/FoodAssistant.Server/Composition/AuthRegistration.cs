using FoodAssistant.Server.Auth;

namespace FoodAssistant.Server.Composition;

/// <summary>Identity, OpenIddict and authorization policies (ADR-0002, ADR-0003). Owned by the auth track.</summary>
public static class AuthRegistration
{
    public static WebApplicationBuilder AddAuth(this WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.AddSessionAuthentication();
        builder.AddTokenServer();
        builder.AddAccessPolicies();
        return builder;
    }

    public static WebApplication UseAuth(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);
        app.UseAuthentication();
        app.UseAuthorization();

        // The OpenID endpoints live outside /api/v1; mapped here so Program.cs stays untouched.
        app.MapConnectEndpoints();
        return app;
    }
}
