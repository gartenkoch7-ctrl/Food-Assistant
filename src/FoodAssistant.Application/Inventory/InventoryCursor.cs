namespace FoodAssistant.Application.Inventory;

/// <summary>Sort position of the inventory overview: next expiry (none last), name, product id.</summary>
public sealed record InventoryCursor(DateOnly? NextExpiry, string Name, Guid ProductId);
