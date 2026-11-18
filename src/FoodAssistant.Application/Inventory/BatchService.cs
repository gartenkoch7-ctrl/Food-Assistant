using FoodAssistant.Application.Common;
using FoodAssistant.Application.Diagnostics;
using FoodAssistant.Application.Households;
using FoodAssistant.Application.ShelfLife;
using FoodAssistant.Contracts.Catalog;
using FoodAssistant.Contracts.Inventory;
using FoodAssistant.Domain.Inventory;
using Microsoft.Extensions.Logging;
using static FoodAssistant.Application.Inventory.InventoryResults;

namespace FoodAssistant.Application.Inventory;

/// <summary>
/// Changes of a stored batch: correct, move (freezes or thaws by the storage type of the target,
/// ADR-0010) and open (docs/kb/vorrat-und-haltbarkeit.md). Every change can be undone.
/// </summary>
public sealed class BatchService(
    IInventoryStore store,
    IShelfLifeRules rules,
    IHouseholdCalendar calendar,
    IHouseholdContext householdContext,
    InventoryCommitter committer,
    ILogger<BatchService> logger)
{
    /// <summary>Amount always; package date and kind only while sealed (F14).</summary>
    public async Task<OperationResult<InventoryActionResponse>> CorrectAsync(
        Guid householdId,
        Guid batchId,
        long? expectedVersion,
        CorrectBatchRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        householdContext.EnsureActive(householdId);
        using var activity = Telemetry.Source.StartActivity("foodassistant.inventory.correct_batch");

        var errors = InventoryValidation.NewErrors();
        var quantity = InventoryValidation.Quantity(request.Quantity, "quantity", errors);
        var kind = InventoryValidation.PackageDate(request.PackageExpiresOn, request.PackageExpiryKind, errors);
        if (errors.Count > 0 || quantity is not { } amount || kind is not { } expiryKind)
        {
            return Finish(activity, OperationResult<InventoryActionResponse>.Invalid(errors, InventoryErrorCodes.ValidationFailed));
        }

        var (context, failure) = await committer.LoadAsync(batchId, expectedVersion, cancellationToken).ConfigureAwait(false);
        if (context is null)
        {
            return Finish(activity, failure!);
        }

        var batch = context.Batch;
        if (!amount.CanConvertTo(batch.Quantity.Unit))
        {
            return Finish(activity, OperationResult<InventoryActionResponse>.RuleViolated(InventoryErrorCodes.UnitMismatch));
        }

        if (batch.State != BatchState.Sealed
            && (request.PackageExpiresOn != batch.PackageExpiresOn || expiryKind != batch.PackageExpiryKind))
        {
            return Finish(activity, OperationResult<InventoryActionResponse>.RuleViolated(InventoryErrorCodes.DateLocked));
        }

        var before = StockBatchSnapshot.From(batch);
        batch.Correct(amount, request.PackageExpiresOn, expiryKind);
        var today = await calendar.GetTodayAsync(cancellationToken).ConfigureAwait(false);
        var result = await committer.ApplyAsync(
            new InventoryAction(InventoryActivityKinds.BatchCorrected, context.ProductId, [BatchStep.Changed(context, before)], StockChanged: true, VersionSent: expectedVersion is not null),
            today,
            created: false,
            cancellationToken).ConfigureAwait(false);
        LogChange(result, batchId, InventoryActivityKinds.BatchCorrected);
        return Finish(activity, result);
    }

    /// <summary>Moves to another location: into the freezer freezes, out of it thaws; thawed food is never refrozen.</summary>
    public async Task<OperationResult<InventoryActionResponse>> MoveAsync(
        Guid householdId,
        Guid batchId,
        long? expectedVersion,
        MoveBatchRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        householdContext.EnsureActive(householdId);
        using var activity = Telemetry.Source.StartActivity("foodassistant.inventory.move_batch");

        var (context, failure) = await committer.LoadAsync(batchId, expectedVersion, cancellationToken).ConfigureAwait(false);
        if (context is null)
        {
            return Finish(activity, failure!);
        }

        var location = await store.GetLocationAsync(request.StorageLocationId, cancellationToken).ConfigureAwait(false);
        if (location is null)
        {
            return Finish(activity, OperationResult<InventoryActionResponse>.NotFound(CatalogErrorCodes.LocationNotFound));
        }

        var batch = context.Batch;
        if (location.Type == StorageType.Frozen && batch.State == BatchState.Thawed)
        {
            return Finish(activity, OperationResult<InventoryActionResponse>.RuleViolated(InventoryErrorCodes.RefreezeNotAllowed));
        }

        // Freezing uses the row (category, frozen), thawing the row (category, target): both are the
        // target's type. A plain relocation ignores the rule.
        var rule = await rules.ResolveAsync(context.ProductId, location.Type, cancellationToken).ConfigureAwait(false);
        var today = await calendar.GetTodayAsync(cancellationToken).ConfigureAwait(false);
        var before = StockBatchSnapshot.From(batch);
        var move = BatchMove.SameType;
        if (TryChange(() => move =BatchMover.Move(batch, location.Type, today.Today, rule)) is { } invalid)
        {
            return Finish(activity, invalid);
        }

        var kind = move switch
        {
            BatchMove.Frozen => InventoryActivityKinds.BatchFrozen,
            BatchMove.Thawed => InventoryActivityKinds.BatchThawed,
            _ => InventoryActivityKinds.BatchMoved,
        };
        var result = await committer.ApplyAsync(
            new InventoryAction(kind, context.ProductId, [BatchStep.Changed(context, before, location.Id)], StockChanged: false, VersionSent: expectedVersion is not null),
            today,
            created: false,
            cancellationToken).ConfigureAwait(false);
        LogChange(result, batchId, kind);
        return Finish(activity, result);
    }

    /// <summary>Opens a sealed batch: min(package date, today + rule "after opening") for its current storage type.</summary>
    public async Task<OperationResult<InventoryActionResponse>> OpenAsync(
        Guid householdId,
        Guid batchId,
        long? expectedVersion,
        CancellationToken cancellationToken)
    {
        householdContext.EnsureActive(householdId);
        using var activity = Telemetry.Source.StartActivity("foodassistant.inventory.open_batch");

        var (context, failure) = await committer.LoadAsync(batchId, expectedVersion, cancellationToken).ConfigureAwait(false);
        if (context is null)
        {
            return Finish(activity, failure!);
        }

        var batch = context.Batch;
        if (batch.State != BatchState.Sealed)
        {
            return Finish(activity, OperationResult<InventoryActionResponse>.RuleViolated(InventoryErrorCodes.InvalidState));
        }

        var rule = await rules.ResolveAsync(context.ProductId, batch.StorageType, cancellationToken).ConfigureAwait(false);
        var today = await calendar.GetTodayAsync(cancellationToken).ConfigureAwait(false);
        var before = StockBatchSnapshot.From(batch);
        if (TryChange(() => batch.Open(today.Today, rule)) is { } invalid)
        {
            return Finish(activity, invalid);
        }

        var result = await committer.ApplyAsync(
            new InventoryAction(InventoryActivityKinds.BatchOpened, context.ProductId, [BatchStep.Changed(context, before)], StockChanged: false, VersionSent: expectedVersion is not null),
            today,
            created: false,
            cancellationToken).ConfigureAwait(false);
        LogChange(result, batchId, InventoryActivityKinds.BatchOpened);
        return Finish(activity, result);
    }

    private void LogChange(OperationResult<InventoryActionResponse> result, Guid batchId, string change)
    {
        if (result.Succeeded)
        {
            InventoryLog.BatchStateChanged(logger, batchId, change);
        }
    }
}
