namespace FoodAssistant.Domain.Shopping;

/// <summary>How an entry came onto the shopping list.</summary>
public enum ShoppingItemOrigin
{
    /// <summary>Added by hand.</summary>
    Manual,

    /// <summary>Added because the stock fell below the product's minimum.</summary>
    MinimumStock,

    /// <summary>Added by a recurring shopping rule.</summary>
    Recurring,

    /// <summary>Added from a meal plan.</summary>
    MealPlan,

    /// <summary>Added by voice input.</summary>
    Voice,

    /// <summary>Imported from a file or another application.</summary>
    Import,
}
