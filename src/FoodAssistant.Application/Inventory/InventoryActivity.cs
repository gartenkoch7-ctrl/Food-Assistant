namespace FoodAssistant.Application.Inventory;

/// <summary>A stored inventory activity, as far as undo needs it.</summary>
public sealed record InventoryActivity(
    Guid Id,
    string Kind,
    Guid? ProductId,
    string? PayloadJson,
    DateTimeOffset OccurredAt,
    DateTimeOffset? UndoneAt);
