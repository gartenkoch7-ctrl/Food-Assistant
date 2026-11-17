namespace FoodAssistant.Server.Composition;

/// <summary>Logging, tracing and metrics setup (AGENTS.md 2.5). Owned by the API/telemetry track.</summary>
public static class TelemetryRegistration
{
    public static WebApplicationBuilder AddServerTelemetry(this WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder;
    }
}
