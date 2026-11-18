namespace FoodAssistant.Domain.Inventory;

/// <summary>How a batch came into the inventory; used for statistics and the activity feed.</summary>
public enum BatchOrigin
{
    /// <summary>Entered by hand.</summary>
    Manual,

    /// <summary>Added after scanning a barcode.</summary>
    Scanned,

    /// <summary>Taken over from a checked-off shopping list ("Einkauf abschließen").</summary>
    Purchase,

    /// <summary>Imported from a file or another application.</summary>
    Import,

    /// <summary>Leftover or home-made dish.</summary>
    Leftover,
}
