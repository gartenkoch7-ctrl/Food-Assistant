using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace FoodAssistant.Application.Diagnostics;

/// <summary>
/// The one ActivitySource and Meter of the server (AGENTS.md 2.5). Activity names follow
/// <c>foodassistant.&lt;feature&gt;.&lt;operation&gt;</c>; every name is listed in docs/kb/telemetrie.md.
/// </summary>
public static class Telemetry
{
    public const string Name = "FoodAssistant";

    public static readonly ActivitySource Source = new(Name);

    public static readonly Meter Meter = new(Name);
}
