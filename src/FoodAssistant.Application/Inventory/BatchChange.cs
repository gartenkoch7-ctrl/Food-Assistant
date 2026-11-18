namespace FoodAssistant.Application.Inventory;

/// <summary>Everything one inventory action stores, saved in one transaction.</summary>
/// <param name="Now">Time of the change; becomes <c>UpdatedAt</c> of every written batch (microsecond precision).</param>
/// <param name="Updated">Changed batches; they must have been loaded through the same store instance.</param>
/// <param name="Added">New batches.</param>
/// <param name="Removed">Batches to delete; they must have been loaded through the same store instance.</param>
/// <param name="Events">Consumption events to record.</param>
/// <param name="UndoneEventIds">Events to mark as undone.</param>
/// <param name="UndoneActivityId">Activity to mark as undone; it must have been loaded through the same store instance.</param>
public sealed record BatchChange(
    DateTimeOffset Now,
    IReadOnlyList<BatchUpdate> Updated,
    IReadOnlyList<NewBatch> Added,
    IReadOnlyList<BatchRemoval> Removed,
    IReadOnlyList<NewConsumptionEvent> Events,
    IReadOnlyList<Guid> UndoneEventIds,
    Guid? UndoneActivityId)
{
    /// <summary>A change without undo bookkeeping.</summary>
    public static BatchChange Of(
        DateTimeOffset now,
        IReadOnlyList<BatchUpdate>? updated = null,
        IReadOnlyList<NewBatch>? added = null,
        IReadOnlyList<BatchRemoval>? removed = null,
        IReadOnlyList<NewConsumptionEvent>? events = null) =>
        new(now, updated ?? [], added ?? [], removed ?? [], events ?? [], [], null);
}
