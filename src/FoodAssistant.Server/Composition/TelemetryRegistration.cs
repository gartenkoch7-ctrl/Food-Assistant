using System.Diagnostics;
using FoodAssistant.Application.Diagnostics;
using FoodAssistant.Server.Api;
using FoodAssistant.Server.Api.Info;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace FoodAssistant.Server.Composition;

/// <summary>Logging, tracing and metrics setup (AGENTS.md 2.5). Owned by the API/telemetry track.</summary>
public static class TelemetryRegistration
{
    /// <summary>
    /// Standard OpenTelemetry variable; the self-hoster sets it to export (ADR-0007). It is the
    /// switch: per-signal variables (<c>OTEL_EXPORTER_OTLP_TRACES_ENDPOINT</c> …) only refine it.
    /// </summary>
    public const string OtlpEndpointVariable = "OTEL_EXPORTER_OTLP_ENDPOINT";

    /// <summary>OpenTelemetry <c>service.name</c> of the server.</summary>
    public const string ServiceName = "foodassistant-server";

    public static WebApplicationBuilder AddServerTelemetry(this WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var openTelemetry = builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(ServiceName, serviceVersion: ServerVersion.Current))
            .WithTracing(tracing => tracing
                .AddSource(Telemetry.Name)
                .AddAspNetCoreInstrumentation(options =>
                {
                    options.Filter = context => !IsHealthCheck(context);
                    options.EnrichWithHttpResponse = RedactPath;
                })
                .AddHttpClientInstrumentation(options => options.EnrichWithHttpRequestMessage = RedactUrl))
            .WithMetrics(metrics => metrics
                .AddMeter(Telemetry.Name)
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation())
            .WithLogging();

        // Without an endpoint nothing leaves the server: the SDK collects, but no exporter is added.
        if (IsOtlpExportEnabled(builder.Configuration))
        {
            openTelemetry.UseOtlpExporter();
        }

        return builder;
    }

    /// <summary><c>true</c> when <see cref="OtlpEndpointVariable"/> is set (environment variables are part of the configuration).</summary>
    public static bool IsOtlpExportEnabled(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        return !string.IsNullOrWhiteSpace(configuration[OtlpEndpointVariable]);
    }

    // AGENTS.md 2.5: no concrete paths in attributes (they may carry ids or user input); the
    // route template ("/api/v1/households/{householdId}/items") says enough. Query strings are
    // already redacted by the instrumentation.
    private static void RedactPath(Activity activity, HttpResponse response) =>
        activity.SetTag("url.path", ApiRequests.RouteTemplate(response.HttpContext));

    // Outgoing calls (e.g. a barcode lookup) keep only scheme, host and port: no path, no user info.
    private static void RedactUrl(Activity activity, HttpRequestMessage request) =>
        activity.SetTag("url.full", request.RequestUri is { IsAbsoluteUri: true } uri
            ? uri.GetComponents(UriComponents.SchemeAndServer, UriFormat.UriEscaped)
            : "(none)");

    // Docker polls /health every few seconds; tracing it would only add noise.
    private static bool IsHealthCheck(HttpContext context) =>
        context.Request.Path.StartsWithSegments("/health", StringComparison.OrdinalIgnoreCase);
}
