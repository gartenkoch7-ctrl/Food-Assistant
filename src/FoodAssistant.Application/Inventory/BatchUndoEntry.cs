using FoodAssistant.Domain.Inventory;

namespace FoodAssistant.Application.Inventory;

/// <summary>Undo data of one batch touched by an action (stored as activity payload).</summary>
/// <param name="BatchId">The batch.</param>
/// <param name="Before">State before the action; null if the action created the batch.</param>
/// <param name="LocationBefore">Placement before the action; null if the action created the batch.</param>
/// <param name="UpdatedAtAfter">
/// <c>UpdatedAt</c> the batch carries after the action, or null if the action deleted it. Undo only
/// proceeds while the batch still carries this value (or is still deleted).
/// </param>
/// <param name="EventId">Consumption event of the action, if any.</param>
public sealed record BatchUndoEntry(
    Guid BatchId,
    StockBatchSnapshot? Before,
    BatchLocationInfo? LocationBefore,
    DateTimeOffset? UpdatedAtAfter,
    Guid? EventId);
