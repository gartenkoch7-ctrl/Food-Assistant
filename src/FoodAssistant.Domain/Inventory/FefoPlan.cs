using FoodAssistant.Domain.Quantities;

namespace FoodAssistant.Domain.Inventory;

/// <summary>Result of <see cref="FefoPlanner.Plan"/>.</summary>
public sealed class FefoPlan
{
    internal FefoPlan(IReadOnlyList<FefoAllocation> allocations, Quantity shortfall)
    {
        Allocations = allocations;
        Shortfall = shortfall;
    }

    /// <summary>Batches to consume, in FEFO order.</summary>
    public IReadOnlyList<FefoAllocation> Allocations { get; }

    /// <summary>Amount the batches could not cover, in the requested unit; zero if fully covered.</summary>
    public Quantity Shortfall { get; }

    /// <summary>True if the batches cover the requested amount.</summary>
    public bool IsFullyCovered => Shortfall.IsZero;
}
