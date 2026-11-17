using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace FoodAssistant.Architecture.Tests.Samples.Fields;

public static class OwnSourceSample
{
    public static readonly ActivitySource Source = new("Sample");
}

public sealed class OwnMeterSample : IDisposable
{
    private readonly Meter _meter = new("Sample");

    public void Dispose() => _meter.Dispose();
}

public static class NoTelemetrySample
{
    public static readonly string Name = "Sample";
}
