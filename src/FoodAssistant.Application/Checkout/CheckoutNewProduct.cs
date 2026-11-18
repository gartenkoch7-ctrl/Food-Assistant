namespace FoodAssistant.Application.Checkout;

/// <summary>A product to create together with its first batch.</summary>
/// <param name="Id">New product id (UUIDv7).</param>
/// <param name="Name">Trimmed name.</param>
/// <param name="CategoryKey">Known category key.</param>
/// <param name="DefaultUnitCode">Known unit code.</param>
public sealed record CheckoutNewProduct(Guid Id, string Name, string CategoryKey, string DefaultUnitCode);
