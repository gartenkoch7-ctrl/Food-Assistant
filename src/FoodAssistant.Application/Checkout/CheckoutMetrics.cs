using System.Diagnostics.Metrics;
using FoodAssistant.Application.Diagnostics;

namespace FoodAssistant.Application.Checkout;

/// <summary>Metrics of completing a shopping list (catalog: docs/kb/telemetrie.md).</summary>
internal static class CheckoutMetrics
{
    private static readonly Counter<long> Batches = Telemetry.Meter.CreateCounter<long>(
        "foodassistant.checkout.batches",
        unit: "{batch}",
        description: "Batches added by completing a shopping list.");

    public static void BatchesAdded(int count)
    {
        if (count > 0)
        {
            Batches.Add(count);
        }
    }
}
