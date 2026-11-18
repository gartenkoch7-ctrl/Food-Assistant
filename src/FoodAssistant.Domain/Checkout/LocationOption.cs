using FoodAssistant.Domain.Inventory;

namespace FoodAssistant.Domain.Checkout;

/// <summary>A storage location of the household as candidate for a new batch.</summary>
public sealed record LocationOption(Guid Id, StorageType Type);
