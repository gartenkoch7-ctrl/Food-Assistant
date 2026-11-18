namespace FoodAssistant.Contracts.Checkout;

/// <summary>A product created while completing a shopping trip (no barcode).</summary>
/// <param name="Name">Product name.</param>
/// <param name="CategoryKey">Key of the fixed category list.</param>
/// <param name="DefaultUnit">Unit code (<c>UnitCodes</c>).</param>
public sealed record NewProductLine(string Name, string CategoryKey, string DefaultUnit);
