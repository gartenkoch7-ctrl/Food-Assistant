using FoodAssistant.Application.Diagnostics;
using FoodAssistant.Server.Composition;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace FoodAssistant.Server.Tests.Api;

public sealed class TelemetryRegistrationTests(ApiTestFactory factory) : IClassFixture<ApiTestFactory>
{
    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("http://otel-collector:4317", true)]
    public void OtlpExport_IsOnlyEnabledWithEndpoint(string? endpoint, bool expected)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [TelemetryRegistration.OtlpEndpointVariable] = endpoint })
            .Build();

        Assert.Equal(expected, TelemetryRegistration.IsOtlpExportEnabled(configuration));
    }

    [Fact]
    public void Server_RegistersTracingAndMetricsForOwnSource()
    {
        Assert.NotNull(factory.Services.GetService<TracerProvider>());
        Assert.NotNull(factory.Services.GetService<MeterProvider>());
        Assert.True(Telemetry.Source.HasListeners());
    }
}
