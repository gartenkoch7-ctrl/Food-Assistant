using FoodAssistant.Application.Common;
using FoodAssistant.Application.Diagnostics;
using FoodAssistant.Application.Households;
using FoodAssistant.Contracts.Inventory;
using FoodAssistant.Domain.Inventory;
using Microsoft.Extensions.Logging;
using static FoodAssistant.Application.Inventory.InventoryResults;

namespace FoodAssistant.Application.Inventory;

/// <summary>
/// Undo of an inventory action (F4): within 24 hours, once, and only while every touched batch is
/// unchanged since (same <c>UpdatedAt</c>) or still deleted. Restores the stored states, re-creates
/// deleted batches with their old id and marks the action and its consumption events as undone.
/// </summary>
public sealed class UndoService(
    IInventoryStore store,
    IHouseholdCalendar calendar,
    IHouseholdContext householdContext,
    InventoryCommitter committer,
    ILogger<UndoService> logger)
{
    /// <summary>How long an action can be undone.</summary>
    public static readonly TimeSpan Window = TimeSpan.FromHours(24);

    public async Task<OperationResult<InventoryActionResponse>> UndoAsync(
        Guid householdId,
        Guid activityId,
        CancellationToken cancellationToken)
    {
        householdContext.EnsureActive(householdId);
        using var activity = Telemetry.Source.StartActivity("foodassistant.inventory.undo");

        var entry = await store.GetActivityAsync(activityId, cancellationToken).ConfigureAwait(false);
        if (entry is null)
        {
            return Finish(activity, OperationResult<InventoryActionResponse>.NotFound(InventoryErrorCodes.ActivityNotFound));
        }

        var now = committer.Now();
        var refusal = entry switch
        {
            { UndoneAt: not null } => "already_undone",
            { PayloadJson: null } => "not_undoable",
            _ when entry.OccurredAt < now - Window => "expired",
            _ => null,
        };
        if (refusal is not null)
        {
            return Finish(activity, Refuse(refusal));
        }

        IReadOnlyList<BatchUndoEntry> entries;
        try
        {
            entries = BatchSnapshotJson.Deserialize(entry.PayloadJson!);
        }
        catch (FormatException)
        {
            return Finish(activity, Refuse("invalid_payload"));
        }

        if (entries.Count == 0)
        {
            return Finish(activity, Refuse("invalid_payload"));
        }

        var updated = new List<BatchUpdate>();
        var added = new List<NewBatch>();
        var removed = new List<BatchRemoval>();
        var restored = new List<BatchContext>();
        foreach (var undo in entries)
        {
            var current = await store.GetBatchAsync(undo.BatchId, cancellationToken).ConfigureAwait(false);
            var unchanged = undo.UpdatedAtAfter is { } after
                ? current is not null && current.UpdatedAt == after
                : current is null;
            if (!unchanged)
            {
                return Finish(activity, Refuse("changed"));
            }

            if (undo is not { Before: { } before, LocationBefore: { } location })
            {
                removed.Add(new BatchRemoval(undo.BatchId, current!.Version));
                continue;
            }

            var batch = StockBatch.Restore(before);
            if (current is null)
            {
                // An emptied product may have been archived since; stock must not come back to it.
                if (await store.GetProductAsync(location.ProductId, cancellationToken).ConfigureAwait(false) is null)
                {
                    return Finish(activity, Refuse("changed"));
                }

                added.Add(new NewBatch(batch, location.ProductId, location.StorageLocationId, location.Origin, location.AddedOn));
            }
            else
            {
                updated.Add(new BatchUpdate(batch, location.StorageLocationId, current.Version));
            }

            restored.Add(new BatchContext(batch, location.ProductId, location.StorageLocationId, location.Origin, location.AddedOn, now, 0));
        }

        var change = new BatchChange(
            now, updated, added, removed, Events: [], [.. entries.Select(e => e.EventId).OfType<Guid>()], activityId);
        var productId = entry.ProductId ?? restored.Select(r => r.ProductId).FirstOrDefault();
        var (result, undoneActivityId) = await committer.CommitAsync(
            InventoryActivityKinds.Undone, entries[0].BatchId, productId, undo: null, change, stockChanged: true, cancellationToken)
            .ConfigureAwait(false);
        if (result.Status != BatchSaveStatus.Saved)
        {
            // Undone or changed by someone else at the same moment.
            return Finish(activity, Refuse("concurrent"));
        }

        InventoryLog.UndoApplied(logger, activityId);
        var today = await calendar.GetTodayAsync(cancellationToken).ConfigureAwait(false);
        var response = new InventoryActionResponse(
            [.. restored.Select(r => BatchDtoMapper.ToDto(r with { Version = result.Versions[r.Batch.Id] }, today))],
            [.. removed.Select(r => r.BatchId)],
            undoneActivityId);
        return Finish(activity, OperationResult<InventoryActionResponse>.Ok(response), batches: entries.Count);
    }

    private OperationResult<InventoryActionResponse> Refuse(string reason)
    {
        InventoryLog.UndoRejected(logger, reason);
        return OperationResult<InventoryActionResponse>.Conflict(InventoryErrorCodes.UndoNotPossible);
    }
}
