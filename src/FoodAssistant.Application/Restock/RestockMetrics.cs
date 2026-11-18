using System.Diagnostics.Metrics;
using FoodAssistant.Application.Diagnostics;

namespace FoodAssistant.Application.Restock;

/// <summary>Metrics of the restock feature (catalog: docs/kb/telemetrie.md).</summary>
internal static class RestockMetrics
{
    /// <summary>Entries added automatically, tagged <c>origin</c>.</summary>
    public static readonly Counter<long> ItemsAdded = Telemetry.Meter.CreateCounter<long>(
        "foodassistant.restock.items_added",
        unit: "{item}",
        description: "Shopping entries added by minimum stock or recurring rules.");

    public static void MinimumStockItemAdded() =>
        ItemsAdded.Add(1, new KeyValuePair<string, object?>("origin", "minimum_stock"));

    public static void RecurringItemAdded() =>
        ItemsAdded.Add(1, new KeyValuePair<string, object?>("origin", "recurring"));
}
