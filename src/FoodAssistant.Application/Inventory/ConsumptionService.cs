using FoodAssistant.Application.Common;
using FoodAssistant.Application.Diagnostics;
using FoodAssistant.Application.Households;
using FoodAssistant.Contracts.Catalog;
using FoodAssistant.Contracts.Common;
using FoodAssistant.Contracts.Inventory;
using FoodAssistant.Domain.Inventory;
using FoodAssistant.Domain.Quantities;
using Microsoft.Extensions.Logging;
using static FoodAssistant.Application.Inventory.InventoryResults;

namespace FoodAssistant.Application.Inventory;

/// <summary>
/// Using and discarding stock (docs/kb/vorrat-und-haltbarkeit.md): one consumption event per batch
/// with the expiry status of that moment; an emptied batch is deleted. Every action can be undone.
/// </summary>
public sealed class ConsumptionService(
    IInventoryStore store,
    IHouseholdCalendar calendar,
    IHouseholdContext householdContext,
    InventoryCommitter committer,
    ILogger<ConsumptionService> logger)
{
    /// <summary>Uses up, partly uses or discards one batch.</summary>
    public async Task<OperationResult<InventoryActionResponse>> ConsumeBatchAsync(
        Guid householdId,
        Guid batchId,
        long? expectedVersion,
        ConsumeBatchRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        householdContext.EnsureActive(householdId);
        using var activity = Telemetry.Source.StartActivity("foodassistant.inventory.consume_batch");

        var errors = InventoryValidation.NewErrors();
        var requested = Validate(request.Kind, request.Quantity, request.Reason, amountForUsedUp: false, errors);
        if (errors.Count > 0)
        {
            return Finish(activity, Invalid(errors));
        }

        var (context, failure) = await committer.LoadAsync(batchId, expectedVersion, cancellationToken).ConfigureAwait(false);
        if (context is null)
        {
            return Finish(activity, failure!);
        }

        var batch = context.Batch;
        var amount = requested ?? batch.Quantity;
        if (!amount.CanConvertTo(batch.Quantity.Unit))
        {
            return Finish(activity, OperationResult<InventoryActionResponse>.RuleViolated(InventoryErrorCodes.UnitMismatch));
        }

        // Partial leaves something; discarding may take less than the rest, never more.
        if ((request.Kind == ConsumptionKindDto.Partial && amount >= batch.Quantity) || amount > batch.Quantity)
        {
            errors["quantity"] = [request.Kind == ConsumptionKindDto.Partial ? "must_be_less_than_remaining" : "exceeds_remaining"];
            return Finish(activity, Invalid(errors));
        }

        var kind = (ConsumptionKind)request.Kind;
        var today = await calendar.GetTodayAsync(cancellationToken).ConfigureAwait(false);
        var step = Consume(context, amount, kind == ConsumptionKind.Discarded ? kind : null, Reason(request.Kind, request.Reason), today);
        var result = await ApplyAsync(kind, context.ProductId, [step], today, expectedVersion is not null, cancellationToken).ConfigureAwait(false);
        return Finish(activity, result, batches: 1);
    }

    /// <summary>
    /// Uses or discards stock of a product "first expired, first out" (batches without date last);
    /// without amount the whole earliest batch.
    /// </summary>
    public async Task<OperationResult<InventoryActionResponse>> ConsumeProductAsync(
        Guid householdId,
        Guid productId,
        ConsumeProductRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        householdContext.EnsureActive(householdId);
        using var activity = Telemetry.Source.StartActivity("foodassistant.inventory.consume_product");

        var errors = InventoryValidation.NewErrors();
        var requested = Validate(request.Kind, request.Quantity, request.Reason, amountForUsedUp: true, errors);
        if (errors.Count > 0)
        {
            return Finish(activity, Invalid(errors));
        }

        if (await store.GetProductAsync(productId, cancellationToken).ConfigureAwait(false) is null)
        {
            return Finish(activity, OperationResult<InventoryActionResponse>.NotFound(CatalogErrorCodes.ProductNotFound));
        }

        // Equal dates: the batch added first goes first.
        var contexts = (await store.GetBatchesOfProductAsync(productId, cancellationToken).ConfigureAwait(false))
            .Where(c => !c.Batch.IsEmpty)
            .OrderBy(c => c.AddedOn)
            .ThenBy(c => c.Batch.Id)
            .ToList();
        IReadOnlyList<FefoAllocation> allocations;
        if (requested is { } amount)
        {
            if (!contexts.Any(c => c.Batch.Quantity.CanConvertTo(amount.Unit)))
            {
                var code = contexts.Count == 0 ? InventoryErrorCodes.NotEnoughStock : InventoryErrorCodes.UnitMismatch;
                return Finish(activity, OperationResult<InventoryActionResponse>.RuleViolated(code));
            }

            var plan = FefoPlanner.Plan(contexts.Select(c => c.Batch), amount);
            if (!plan.IsFullyCovered)
            {
                return Finish(activity, OperationResult<InventoryActionResponse>.RuleViolated(InventoryErrorCodes.NotEnoughStock));
            }

            allocations = plan.Allocations;
        }
        else
        {
            var earliest = contexts.OrderBy(c => c.Batch.ExpiresOn is null).ThenBy(c => c.Batch.ExpiresOn).FirstOrDefault();
            if (earliest is null)
            {
                return Finish(activity, OperationResult<InventoryActionResponse>.RuleViolated(InventoryErrorCodes.NotEnoughStock));
            }

            allocations = [new FefoAllocation(earliest.Batch.Id, earliest.Batch.Quantity)];
        }

        var kind = (ConsumptionKind)request.Kind;
        var today = await calendar.GetTodayAsync(cancellationToken).ConfigureAwait(false);
        var steps = allocations
            .Select(a => Consume(
                contexts.First(c => c.Batch.Id == a.BatchId),
                a.Quantity,
                kind == ConsumptionKind.Discarded ? kind : null,
                Reason(request.Kind, request.Reason),
                today))
            .ToList();
        var result = await ApplyAsync(kind, productId, steps, today, versionSent: false, cancellationToken).ConfigureAwait(false);
        return Finish(activity, result, batches: steps.Count);
    }

    // Shared field rules of both requests; returns the requested amount, if any.
    private static Quantity? Validate(
        ConsumptionKindDto kind,
        QuantityDto? quantity,
        ConsumptionReasonDto? reason,
        bool amountForUsedUp,
        Dictionary<string, string[]> errors)
    {
        InventoryValidation.Defined(kind, "kind", errors);
        if (reason is { } given)
        {
            InventoryValidation.Defined(given, "reason", errors);
        }
        else if (kind == ConsumptionKindDto.Discarded)
        {
            errors["reason"] = ["required"];
        }

        if (quantity is null)
        {
            if (kind == ConsumptionKindDto.Partial)
            {
                errors["quantity"] = ["required"];
            }

            return null;
        }

        // Using up one batch takes all of it, so an amount there would be ambiguous; for a product it
        // is the amount taken first-expired-first-out.
        if (kind == ConsumptionKindDto.UsedUp && !amountForUsedUp)
        {
            errors["quantity"] = ["must_be_empty"];
            return null;
        }

        return InventoryValidation.Quantity(quantity, "quantity", errors);
    }

    // A reason only belongs to a disposal.
    private static ConsumptionReason? Reason(ConsumptionKindDto kind, ConsumptionReasonDto? reason) =>
        kind == ConsumptionKindDto.Discarded ? (ConsumptionReason?)reason : null;

    private BatchStep Consume(
        BatchContext context,
        Quantity amount,
        ConsumptionKind? fixedKind,
        ConsumptionReason? reason,
        HouseholdToday today)
    {
        var batch = context.Batch;
        var status = BatchDtoMapper.Evaluate(batch, today).Status;
        var before = StockBatchSnapshot.From(batch);

        // The database keeps three decimals (decimal(12,3)). Converting units can produce more
        // (1 kg - 999.9 g), so take up to the next 0.001 of the batch's unit, at most the rest: the
        // stored rest is then exact and a rest below 0.001 empties (and deletes) the batch.
        var converted = amount.ConvertTo(batch.Quantity.Unit);
        var taken = new Quantity(Math.Min(Math.Ceiling(converted.Value * 1000m) / 1000m, batch.Quantity.Value), batch.Quantity.Unit);
        batch.Consume(taken);
        var kind = fixedKind ?? (batch.IsEmpty ? ConsumptionKind.UsedUp : ConsumptionKind.Partial);
        var consumption = new NewConsumptionEvent(
            Guid.CreateVersion7(), context.ProductId, batch.Id, kind, reason, taken, status, committer.UserId, today.Today);
        return BatchStep.Changed(context, before, consumption: consumption);
    }

    private async Task<OperationResult<InventoryActionResponse>> ApplyAsync(
        ConsumptionKind kind,
        Guid productId,
        IReadOnlyList<BatchStep> steps,
        HouseholdToday today,
        bool versionSent,
        CancellationToken cancellationToken)
    {
        var activityKind = kind switch
        {
            ConsumptionKind.UsedUp => InventoryActivityKinds.UsedUp,
            ConsumptionKind.Partial => InventoryActivityKinds.ConsumedPartly,
            _ => InventoryActivityKinds.Discarded,
        };
        var result = await committer.ApplyAsync(
            new InventoryAction(activityKind, productId, steps, StockChanged: true, versionSent), today, created: false, cancellationToken)
            .ConfigureAwait(false);
        if (result.Succeeded)
        {
            foreach (var consumption in steps.Select(s => s.Event!))
            {
                InventoryLog.BatchConsumed(logger, consumption.BatchId, consumption.Kind);
                InventoryMetrics.Consumed(consumption.Kind, consumption.Reason);
            }
        }

        return result;
    }

    private static OperationResult<InventoryActionResponse> Invalid(Dictionary<string, string[]> errors) =>
        OperationResult<InventoryActionResponse>.Invalid(errors, InventoryErrorCodes.ValidationFailed);
}
