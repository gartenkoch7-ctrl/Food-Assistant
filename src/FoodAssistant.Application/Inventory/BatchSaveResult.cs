namespace FoodAssistant.Application.Inventory;

/// <summary>Result of <see cref="IInventoryStore.SaveAsync"/>.</summary>
/// <param name="Status">Whether the change was stored.</param>
/// <param name="Versions">New version of every updated or added batch (empty unless saved).</param>
public sealed record BatchSaveResult(BatchSaveStatus Status, IReadOnlyDictionary<Guid, long> Versions);
