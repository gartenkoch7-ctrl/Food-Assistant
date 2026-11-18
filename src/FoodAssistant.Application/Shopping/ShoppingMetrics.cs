using System.Diagnostics.Metrics;
using FoodAssistant.Application.Diagnostics;
using FoodAssistant.Domain.Shopping;

namespace FoodAssistant.Application.Shopping;

/// <summary>Metrics of the shopping feature (catalog: docs/kb/telemetrie.md).</summary>
internal static class ShoppingMetrics
{
    /// <summary>Conflicts resolved by the rules of ADR-0005, tagged <c>outcome</c>.</summary>
    public static readonly Counter<long> Conflicts = Telemetry.Meter.CreateCounter<long>(
        "foodassistant.shopping.conflicts",
        unit: "{conflict}",
        description: "Shopping item conflicts resolved by outcome.");

    /// <summary>Entries checked off.</summary>
    public static readonly Counter<long> ItemsChecked = Telemetry.Meter.CreateCounter<long>(
        "foodassistant.shopping.items.checked",
        unit: "{item}",
        description: "Shopping items checked off.");

    public static void ConflictResolved(ShoppingConflictOutcome outcome) =>
        Conflicts.Add(1, new KeyValuePair<string, object?>("outcome", outcome.ToString()));

    public static void ItemChecked() => ItemsChecked.Add(1);
}
