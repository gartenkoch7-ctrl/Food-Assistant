using System.Diagnostics.Metrics;
using FoodAssistant.Application.Diagnostics;
using FoodAssistant.Domain.Inventory;

namespace FoodAssistant.Application.Inventory;

/// <summary>Metrics of the inventory feature (catalog: docs/kb/telemetrie.md).</summary>
internal static class InventoryMetrics
{
    private static readonly Counter<long> Consumptions = Telemetry.Meter.CreateCounter<long>(
        "foodassistant.inventory.consumptions",
        unit: "{event}",
        description: "Consumption events by kind and discard reason.");

    private static readonly Counter<long> BatchesAdded = Telemetry.Meter.CreateCounter<long>(
        "foodassistant.inventory.batches.added",
        unit: "{batch}",
        description: "Batches added by origin.");

    public static void Consumed(ConsumptionKind kind, ConsumptionReason? reason) =>
        Consumptions.Add(
            1,
            new KeyValuePair<string, object?>("kind", kind.ToString()),
            new KeyValuePair<string, object?>("reason", reason?.ToString() ?? "none"));

    public static void BatchAdded(BatchOrigin origin) =>
        BatchesAdded.Add(1, new KeyValuePair<string, object?>("origin", origin.ToString()));
}
