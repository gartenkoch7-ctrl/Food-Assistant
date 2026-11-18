namespace FoodAssistant.UI.Checkout;

/// <summary>Problem of one checkout line: the field it concerns (JSON name, e.g. <c>storageLocationId</c>) and its text.</summary>
public sealed record CheckoutLineError(string Field, string Text)
{
    public const string LocationField = "storageLocationId";

    public const string ProductField = "productId";

    /// <summary>The line's location select shows it.</summary>
    public bool IsLocation => Field == LocationField;

    /// <summary>Shown at the product choice (existing or new product).</summary>
    public bool IsProduct => Field is ProductField or "newProduct";
}
