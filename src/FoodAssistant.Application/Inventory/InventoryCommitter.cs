using FoodAssistant.Application.Activity;
using FoodAssistant.Application.Common;
using FoodAssistant.Application.Households;
using FoodAssistant.Contracts.Common;
using FoodAssistant.Contracts.Inventory;
using Microsoft.Extensions.Logging;

namespace FoodAssistant.Application.Inventory;

/// <summary>
/// The common end of every inventory action: activity entry with undo data, one save, then the
/// stock-changed hooks. Shared by <see cref="BatchService"/>, <see cref="ConsumptionService"/> and
/// <see cref="UndoService"/>.
/// </summary>
public sealed class InventoryCommitter(
    IInventoryStore store,
    IActivityLog activityLog,
    IHouseholdContext householdContext,
    IEnumerable<IStockChangedHandler> stockHandlers,
    TimeProvider time,
    ILogger<InventoryCommitter> logger)
{
    /// <summary>The signed-in person of the request.</summary>
    public Guid? UserId => householdContext.Membership?.UserId;

    /// <summary>
    /// Current time cut to microseconds: PostgreSQL stores <c>timestamptz</c> with microsecond
    /// precision, and undo compares the stored <c>UpdatedAt</c> with the value kept in the payload.
    /// </summary>
    public DateTimeOffset Now()
    {
        var now = time.GetUtcNow();
        return now.AddTicks(-(now.UtcTicks % 10));
    }

    /// <summary>Loads a batch; a missing one is 404, a stale <paramref name="expectedVersion"/> 412.</summary>
    public async Task<(BatchContext? Batch, OperationResult<InventoryActionResponse>? Failure)> LoadAsync(
        Guid batchId,
        long? expectedVersion,
        CancellationToken cancellationToken)
    {
        var batch = await store.GetBatchAsync(batchId, cancellationToken).ConfigureAwait(false);
        if (batch is null)
        {
            return (null, OperationResult<InventoryActionResponse>.NotFound(InventoryErrorCodes.BatchNotFound));
        }

        return expectedVersion is { } expected && expected != batch.Version
            ? (null, OperationResult<InventoryActionResponse>.VersionMismatch(batch.Version, null))
            : (batch, null);
    }

    /// <summary>Stores <paramref name="change"/> with an activity entry and runs the hooks on success.</summary>
    /// <param name="kind">Activity kind (<see cref="InventoryActivityKinds"/>).</param>
    /// <param name="subjectId">Batch the activity is about (the first one for several).</param>
    /// <param name="productId">Product concerned.</param>
    /// <param name="undo">Undo data, or null if the action cannot be undone.</param>
    /// <param name="change">What to store.</param>
    /// <param name="stockChanged">Whether amounts changed or batches came or went (hooks run only then).</param>
    /// <param name="cancellationToken">Cancellation of the request.</param>
    public async Task<(BatchSaveResult Result, Guid ActivityId)> CommitAsync(
        string kind,
        Guid subjectId,
        Guid productId,
        IReadOnlyList<BatchUndoEntry>? undo,
        BatchChange change,
        bool stockChanged,
        CancellationToken cancellationToken)
    {
        var payload = undo is null ? null : BatchSnapshotJson.Serialize(undo);
        var activityId = activityLog.Add(new ActivityEntry(
            ActivityArea.Inventory, kind, subjectId, productId, UserId, payload, SubjectVersionAfter: null));

        var result = await store.SaveAsync(change, cancellationToken).ConfigureAwait(false);
        if (result.Status == BatchSaveStatus.Saved && stockChanged)
        {
            await NotifyStockChangedAsync(productId, cancellationToken).ConfigureAwait(false);
        }

        return (result, activityId);
    }

    /// <summary>
    /// Stores an action with its undo data and answers with the touched batches. A batch changed
    /// since loading answers 412 if the client sent its version, otherwise 409; a duplicate id 409.
    /// </summary>
    /// <param name="action">The action.</param>
    /// <param name="today">Household today for the status of the returned batches.</param>
    /// <param name="created">Answer 201 instead of 200.</param>
    /// <param name="cancellationToken">Cancellation of the request.</param>
    public async Task<OperationResult<InventoryActionResponse>> ApplyAsync(
        InventoryAction action,
        HouseholdToday today,
        bool created,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(action);
        var now = Now();
        var steps = action.Steps;
        var change = new BatchChange(
            now,
            [.. steps.Where(s => s.Original is not null && !s.Removes)
                .Select(s => new BatchUpdate(s.Batch, s.StorageLocationId, s.Original!.Version))],
            [.. steps.Where(s => s.Original is null)
                .Select(s => new NewBatch(s.Batch, s.ProductId, s.StorageLocationId, s.Origin, s.AddedOn))],
            [.. steps.Where(s => s.Removes).Select(s => new BatchRemoval(s.Batch.Id, s.Original!.Version))],
            [.. steps.Select(s => s.Event).OfType<NewConsumptionEvent>()],
            [],
            null);
        var undo = steps.Select(s => new BatchUndoEntry(
            s.Batch.Id,
            s.Before,
            s.Original is { } o ? new BatchLocationInfo(o.ProductId, o.StorageLocationId, o.Origin, o.AddedOn) : null,
            s.Removes ? null : now,
            s.Event?.Id)).ToList();

        var (result, activityId) = await CommitAsync(
            action.Kind, steps[0].Batch.Id, action.ProductId, undo, change, action.StockChanged, cancellationToken)
            .ConfigureAwait(false);
        if (result.Status != BatchSaveStatus.Saved)
        {
            return await FailureAsync(result.Status, action, cancellationToken).ConfigureAwait(false);
        }

        var response = new InventoryActionResponse(
            [.. steps.Where(s => !s.Removes).Select(s => BatchDtoMapper.ToDto(
                new BatchContext(s.Batch, s.ProductId, s.StorageLocationId, s.Origin, s.AddedOn, now, result.Versions[s.Batch.Id]),
                today))],
            [.. steps.Where(s => s.Removes).Select(s => s.Batch.Id)],
            activityId);
        return created
            ? OperationResult<InventoryActionResponse>.Created(response)
            : OperationResult<InventoryActionResponse>.Ok(response);
    }

    private async Task<OperationResult<InventoryActionResponse>> FailureAsync(
        BatchSaveStatus status,
        InventoryAction action,
        CancellationToken cancellationToken)
    {
        if (status == BatchSaveStatus.Duplicate)
        {
            return OperationResult<InventoryActionResponse>.Conflict(InventoryErrorCodes.BatchExists);
        }

        // The client sent the version of this one batch: answer like a stale If-Match (412 + ETag).
        if (action is { VersionSent: true, Steps: [{ Original: { } original }] })
        {
            var current = await store.GetBatchAsync(original.Batch.Id, cancellationToken).ConfigureAwait(false);
            return current is null
                ? OperationResult<InventoryActionResponse>.NotFound(InventoryErrorCodes.BatchNotFound)
                : OperationResult<InventoryActionResponse>.VersionMismatch(current.Version, null);
        }

        return OperationResult<InventoryActionResponse>.Conflict(ErrorCodes.Conflict);
    }

    // Hooks run after the commit; a failing one is logged and never fails the action (IStockChangedHandler).
    private async Task NotifyStockChangedAsync(Guid productId, CancellationToken cancellationToken)
    {
        var householdId = householdContext.HouseholdId
            ?? throw new InvalidOperationException("Inventory actions need an active household.");
        foreach (var handler in stockHandlers)
        {
            try
            {
                await handler.OnStockChangedAsync(householdId, [productId], cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                InventoryLog.StockHandlerFailed(logger, exception);
            }
        }
    }
}
