using FoodAssistant.Application.Diagnostics;

namespace FoodAssistant.Architecture.Tests;

/// <summary>
/// Telemetry rules of AGENTS.md 2.5 checked by reflection over all src/ assemblies. As long as
/// no [LoggerMessage] method exists these pass on the empty set; the check logic itself is
/// covered by <see cref="TelemetryRulesTests"/>.
/// </summary>
public sealed class TelemetryConventionTests
{
    /// <summary>
    /// EventId ranges per project or feature (docs/plan/m00-grundgeruest.md). Add a row when a new
    /// feature or project gets its range; the longest matching prefix wins.
    /// </summary>
    private static readonly EventIdRange[] s_ranges =
    [
        new("FoodAssistant.Server.Api", 1000, 1099),
        new("FoodAssistant.Server.Auth", 1100, 1199),
        new("FoodAssistant.Server.Components", 1200, 1299),
        new("FoodAssistant.Infrastructure", 2000, 2199),
        new("FoodAssistant.Application", 3000, 3099),
        new("FoodAssistant.UI", 4000, 4099),
        new("FoodAssistant.Client", 4000, 4099),
        new("FoodAssistant.Web.Client", 4000, 4099),
    ];

    private static readonly IReadOnlyList<Type> s_types = TelemetryRules.TypesOf(Layers.AllAssemblies);

    [Fact]
    public void LoggerMessageMethods_LiveInStaticClassesNamedLog() =>
        ArchAssert.Satisfied(
            TelemetryRules.MisplacedLogMethods(s_types),
            "[LoggerMessage] methods belong in a static partial class whose name ends with Log (one *Log.cs per feature)");

    [Fact]
    public void LoggerMessageMethods_DeclareAnExplicitEventId() =>
        ArchAssert.Satisfied(
            TelemetryRules.LogMethodsWithoutEventId(s_types),
            "[LoggerMessage] needs an explicit EventId from the project's range");

    [Fact]
    public void EventIds_AreUniqueAcrossAllAssemblies() =>
        ArchAssert.Satisfied(TelemetryRules.DuplicateEventIds(s_types), "EventIds must be unique");

    [Fact]
    public void EventIds_LieInTheRangeOfTheirProject() =>
        ArchAssert.Satisfied(
            TelemetryRules.OutOfRangeEventIds(s_types, s_ranges),
            "EventIds must stay in the range of their project or feature");

    [Fact]
    public void ActivitySourceAndMeter_ExistOnlyInTelemetry() =>
        ArchAssert.Satisfied(
            TelemetryRules.ForeignTelemetryFields(s_types, typeof(Telemetry)),
            "Use Telemetry.Source and Telemetry.Meter instead of creating your own");

    [Fact]
    public void EventIdRanges_DoNotOverlapAcrossDifferentProjects()
    {
        var overlaps = from a in s_ranges
                       from b in s_ranges
                       where string.CompareOrdinal(a.Prefix, b.Prefix) < 0
                           && a.Min <= b.Max && b.Min <= a.Max
                           && !IsShared(a, b)
                       select $"{a.Prefix} and {b.Prefix}";

        ArchAssert.Satisfied([.. overlaps], "Ranges of different projects must not overlap");
    }

    // UI, Client and Web.Client deliberately share 4000-4099 (plan M0); uniqueness is still enforced per EventId.
    private static bool IsShared(EventIdRange a, EventIdRange b) =>
        a.Min == b.Min && a.Max == b.Max && a.Min >= 4000;
}
