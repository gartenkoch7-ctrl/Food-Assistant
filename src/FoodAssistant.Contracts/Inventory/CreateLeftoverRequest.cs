using FoodAssistant.Contracts.Common;

namespace FoodAssistant.Contracts.Inventory;

/// <summary>Records a leftover or home-made dish; its date comes from the rule "as leftover".</summary>
public sealed record CreateLeftoverRequest(Guid? Id, Guid ProductId, Guid StorageLocationId, QuantityDto Quantity);
