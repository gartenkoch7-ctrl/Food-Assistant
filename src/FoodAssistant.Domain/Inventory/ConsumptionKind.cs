namespace FoodAssistant.Domain.Inventory;

/// <summary>Kind of a consumption event (docs/kb/vorrat-und-haltbarkeit.md).</summary>
public enum ConsumptionKind
{
    /// <summary>The whole batch was used ("aufgebraucht").</summary>
    UsedUp,

    /// <summary>Part of the batch was used ("teilweise verbraucht").</summary>
    Partial,

    /// <summary>Thrown away ("entsorgt"); comes with a <see cref="ConsumptionReason"/>.</summary>
    Discarded,
}
