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
        new("FoodAssistant.Server.Sync", 1300, 1324),
        new("FoodAssistant.Infrastructure", 2000, 2199),
        new("FoodAssistant.Infrastructure.Sync", 2200, 2249),
        new("FoodAssistant.Application", 3000, 3099),
        new("FoodAssistant.UI", 4000, 4099),
        new("FoodAssistant.Client", 4000, 4099),
        new("FoodAssistant.Web.Client", 4000, 4099),
        new("FoodAssistant.Server.Activities", 1325, 1349),
        new("FoodAssistant.Infrastructure.Activity", 2250, 2299),
        new("FoodAssistant.Application.Activity", 3150, 3199),
        new("FoodAssistant.UI.Activity", 4150, 4199),
        new("FoodAssistant.Client.Activity", 4150, 4199),
        new("FoodAssistant.Server.Households", 1350, 1374),
        new("FoodAssistant.UI.Session", 4200, 4249),
        new("FoodAssistant.UI.Accounts", 4200, 4249),
        new("FoodAssistant.Client.Session", 4200, 4249),
        new("FoodAssistant.Client.Accounts", 4200, 4249),
        new("FoodAssistant.Server.Catalog", 1375, 1399),
        new("FoodAssistant.Infrastructure.Catalog", 2300, 2349),
        new("FoodAssistant.Application.Catalog", 3200, 3249),
        new("FoodAssistant.UI.Catalog", 4250, 4299),
        new("FoodAssistant.Client.Catalog", 4250, 4299),
        new("FoodAssistant.Application.Sync", 3100, 3149),
        new("FoodAssistant.UI.Sync", 4100, 4149),
        new("FoodAssistant.Client.Sync", 4100, 4149),
        new("FoodAssistant.Server.ShelfLife", 1400, 1424),
        new("FoodAssistant.Infrastructure.ShelfLife", 2350, 2399),
        new("FoodAssistant.Application.ShelfLife", 3250, 3299),
        new("FoodAssistant.UI.ShelfLife", 4300, 4349),
        new("FoodAssistant.Client.ShelfLife", 4300, 4349),
        new("FoodAssistant.Server.Inventory", 1425, 1449),
        new("FoodAssistant.Infrastructure.Inventory", 2400, 2449),
        new("FoodAssistant.Application.Inventory", 3300, 3349),
        new("FoodAssistant.UI.Inventory", 4350, 4399),
        new("FoodAssistant.Client.Inventory", 4350, 4399),
        new("FoodAssistant.Server.FoodFacts", 1450, 1474),
        new("FoodAssistant.Infrastructure.FoodFacts", 2450, 2499),
        new("FoodAssistant.Application.FoodFacts", 3350, 3399),
        new("FoodAssistant.UI.Scan", 4400, 4449),
        new("FoodAssistant.Client.Scan", 4400, 4449),
        new("FoodAssistant.Server.Shopping", 1475, 1499),
        new("FoodAssistant.Infrastructure.Shopping", 2500, 2549),
        new("FoodAssistant.Application.Shopping", 3400, 3449),
        new("FoodAssistant.UI.Shopping", 4450, 4499),
        new("FoodAssistant.Client.Shopping", 4450, 4499),
        new("FoodAssistant.Server.Stores", 1500, 1524),
        new("FoodAssistant.Infrastructure.Stores", 2550, 2599),
        new("FoodAssistant.Application.Stores", 3450, 3499),
        new("FoodAssistant.UI.Stores", 4500, 4549),
        new("FoodAssistant.Client.Stores", 4500, 4549),
        new("FoodAssistant.Server.Checkout", 1525, 1549),
        new("FoodAssistant.Infrastructure.Checkout", 2600, 2649),
        new("FoodAssistant.Application.Checkout", 3500, 3549),
        new("FoodAssistant.UI.Checkout", 4550, 4599),
        new("FoodAssistant.Client.Checkout", 4550, 4599),
        new("FoodAssistant.Server.Restock", 1550, 1574),
        new("FoodAssistant.Infrastructure.Restock", 2650, 2699),
        new("FoodAssistant.Application.Restock", 3550, 3599),
        new("FoodAssistant.UI.Restock", 4600, 4649),
        new("FoodAssistant.Client.Restock", 4600, 4649),
        new("FoodAssistant.Server.Notifications", 1575, 1599),
        new("FoodAssistant.Infrastructure.Notifications", 2700, 2749),
        new("FoodAssistant.Application.Notifications", 3600, 3649),
        new("FoodAssistant.UI.Notifications", 4650, 4699),
        new("FoodAssistant.Client.Notifications", 4650, 4699),
        new("FoodAssistant.Server.Statistics", 1600, 1624),
        new("FoodAssistant.Infrastructure.Statistics", 2750, 2799),
        new("FoodAssistant.Application.Statistics", 3650, 3699),
        new("FoodAssistant.UI.Statistics", 4700, 4749),
        new("FoodAssistant.Client.Statistics", 4700, 4749),
        new("FoodAssistant.Server.Labels", 1625, 1649),
        new("FoodAssistant.Infrastructure.Labels", 2800, 2849),
        new("FoodAssistant.Application.Labels", 3700, 3749),
        new("FoodAssistant.UI.Labels", 4750, 4799),
        new("FoodAssistant.Client.Labels", 4750, 4799),
        new("FoodAssistant.Server.Import", 1650, 1674),
        new("FoodAssistant.Infrastructure.Import", 2850, 2899),
        new("FoodAssistant.Application.Import", 3750, 3799),
        new("FoodAssistant.UI.Import", 4800, 4849),
        new("FoodAssistant.Client.Import", 4800, 4849),
        new("FoodAssistant.Server.Backup", 1675, 1699),
        new("FoodAssistant.Infrastructure.Backup", 2900, 2949),
        new("FoodAssistant.Application.Backup", 3800, 3849),
        new("FoodAssistant.Server.Export", 1700, 1724),
        new("FoodAssistant.Infrastructure.Export", 2950, 2999),
        new("FoodAssistant.Application.Export", 3850, 3899),
        new("FoodAssistant.UI.Export", 4850, 4899),
        new("FoodAssistant.Client.Export", 4850, 4899),
        new("FoodAssistant.Mobile", 4900, 4999),
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
