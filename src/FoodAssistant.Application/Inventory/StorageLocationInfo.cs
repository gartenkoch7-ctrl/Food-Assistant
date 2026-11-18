using FoodAssistant.Domain.Inventory;

namespace FoodAssistant.Application.Inventory;

/// <summary>A storage location as the inventory needs it: id and type.</summary>
public sealed record StorageLocationInfo(Guid Id, StorageType Type);
