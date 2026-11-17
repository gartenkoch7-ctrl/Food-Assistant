namespace FoodAssistant.Domain.Inventory;

/// <summary>State of a <see cref="StockBatch"/> (docs/kb/vorrat-und-haltbarkeit.md).</summary>
public enum BatchState
{
    /// <summary>Unopened package ("verschlossen").</summary>
    Sealed,

    /// <summary>Opened package ("geöffnet").</summary>
    Opened,

    /// <summary>Frozen by the household ("eingefroren").</summary>
    Frozen,

    /// <summary>Thawed after freezing ("aufgetaut").</summary>
    Thawed,

    /// <summary>Leftover or home-made ("Rest/selbst gemacht").</summary>
    Leftover,
}
