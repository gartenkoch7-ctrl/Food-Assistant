using FoodAssistant.Application.Diagnostics;

namespace FoodAssistant.Application.Tests;

public sealed class TelemetryTests
{
    [Fact]
    public void SourceAndMeter_UseProductName()
    {
        Assert.Equal(Telemetry.Name, Telemetry.Source.Name);
        Assert.Equal(Telemetry.Name, Telemetry.Meter.Name);
    }
}
