namespace FoodAssistant.Server.Composition;

/// <summary>API conventions of ADR-0004: errors, idempotency, OpenAPI. Owned by the API/telemetry track.</summary>
public static class ApiRegistration
{
    public static WebApplicationBuilder AddApi(this WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder;
    }

    public static WebApplication UseApi(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);
        return app;
    }
}
