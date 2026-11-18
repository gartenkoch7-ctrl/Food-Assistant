namespace FoodAssistant.Domain.Inventory;

/// <summary>What the minimum-stock check wants done for a product.</summary>
public enum RestockKind
{
    /// <summary>Nothing to do.</summary>
    None,

    /// <summary>Put the product on the default shopping list and remember that it is there.</summary>
    AddToList,

    /// <summary>Only remember that a restock is under way; the product already is on a list.</summary>
    Arm,

    /// <summary>Forget the memory: the stock is sufficient again (or no minimum is set any more).</summary>
    Disarm,
}
