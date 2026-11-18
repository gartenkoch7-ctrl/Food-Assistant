using FoodAssistant.Application.Catalog;
using FoodAssistant.Application.Common;
using FoodAssistant.Application.Diagnostics;
using FoodAssistant.Application.Households;
using FoodAssistant.Application.Inventory;
using FoodAssistant.Application.ShelfLife;
using FoodAssistant.Application.Shopping;
using FoodAssistant.Contracts.Checkout;
using FoodAssistant.Contracts.Common;
using FoodAssistant.Contracts.Inventory;
using FoodAssistant.Domain.Catalog;
using FoodAssistant.Domain.Checkout;
using FoodAssistant.Domain.Inventory;
using FoodAssistant.Domain.Quantities;
using Microsoft.Extensions.Logging;

namespace FoodAssistant.Application.Checkout;

/// <summary>
/// Completing a shopping list (docs/kb/einkauf.md "Abhaken und Einkauf abschließen"): suggestions
/// for every checked entry, then batches and removal of the entries in one transaction.
/// </summary>
public sealed class CheckoutService(
    ICheckoutStore store,
    CheckoutPlanner planner,
    IShelfLifeRules rules,
    IHouseholdCalendar calendar,
    IHouseholdContext householdContext,
    IEnumerable<IStockChangedHandler> stockHandlers,
    TimeProvider time,
    ILogger<CheckoutService> logger)
{
    /// <summary>Suggested amount, location and date for every checked entry of the list.</summary>
    public async Task<OperationResult<CheckoutProposalDto>> ProposeAsync(
        Guid householdId,
        Guid listId,
        CancellationToken cancellationToken)
    {
        householdContext.EnsureActive(householdId);
        var data = await store.LoadAsync(listId, [], cancellationToken).ConfigureAwait(false);
        if (data is null)
        {
            return OperationResult<CheckoutProposalDto>.NotFound(CheckoutErrorCodes.ListNotFound);
        }

        var today = (await calendar.GetTodayAsync(cancellationToken).ConfigureAwait(false)).Today;
        var entries = new List<CheckoutEntryDto>(data.Items.Count);
        foreach (var item in data.Items)
        {
            entries.Add(await ProposeAsync(item, data, today, cancellationToken).ConfigureAwait(false));
        }

        return OperationResult<CheckoutProposalDto>.Ok(new CheckoutProposalDto(listId, entries));
    }

    /// <summary>
    /// Books every line as a batch (or skips it) and removes the entries from the list. Entries that
    /// changed since the client loaded them are left alone and reported in <c>staleItemIds</c>.
    /// </summary>
    public async Task<OperationResult<CheckoutResultDto>> CompleteAsync(
        Guid householdId,
        Guid listId,
        CompleteCheckoutRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        householdContext.EnsureActive(householdId);
        using var activity = Telemetry.Source.StartActivity("foodassistant.checkout.complete");

        var result = await CompleteCoreAsync(listId, request, cancellationToken).ConfigureAwait(false);
        activity?.SetTag("foodassistant.outcome", result.Status.ToString());
        activity?.SetTag("foodassistant.batches", result.Value?.BatchesCreated ?? 0);
        return result;
    }

    private async Task<OperationResult<CheckoutResultDto>> CompleteCoreAsync(
        Guid listId,
        CompleteCheckoutRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Lines is { Count: 0 })
        {
            return OperationResult<CheckoutResultDto>.RuleViolated(CheckoutErrorCodes.NothingToComplete);
        }

        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        if (request.Lines is null)
        {
            errors["lines"] = ["required"];
        }

        if (request.Lines is null || CheckoutValidation.Parse(request.Lines, errors) is not { } lines)
        {
            return OperationResult<CheckoutResultDto>.Invalid(errors, CheckoutErrorCodes.ValidationFailed);
        }

        var assigned = lines.Select(l => l.Request.ProductId).OfType<Guid>().Distinct().ToList();
        var data = await store.LoadAsync(listId, assigned, cancellationToken).ConfigureAwait(false);
        if (data is null)
        {
            return OperationResult<CheckoutResultDto>.NotFound(CheckoutErrorCodes.ListNotFound);
        }

        var today = (await calendar.GetTodayAsync(cancellationToken).ConfigureAwait(false)).Today;
        var now = ShoppingTime.Now(time);
        var plan = await planner.PlanAsync(lines, data, today, now, cancellationToken).ConfigureAwait(false);
        if (plan.Errors.Count > 0)
        {
            return OperationResult<CheckoutResultDto>.Invalid(plan.Errors, CheckoutErrorCodes.ValidationFailed);
        }

        var committed = await store.CommitAsync(new CheckoutCommit(listId, now, today, plan.Lines), cancellationToken)
            .ConfigureAwait(false);
        if (!committed.Succeeded || committed.Value is not { } stored)
        {
            return committed.Status == OperationStatus.NotFound
                ? OperationResult<CheckoutResultDto>.NotFound(CheckoutErrorCodes.ListNotFound)
                : OperationResult<CheckoutResultDto>.Conflict(ErrorCodes.Conflict);
        }

        var batches = stored.Applied.Select(l => l.Batch).OfType<CheckoutBatch>().ToList();
        var stale = plan.StaleItemIds.Concat(stored.StaleItemIds).ToList();
        Report(listId, stored, batches, stale.Count);
        await NotifyStockChangedAsync([.. batches.Select(b => b.ProductId).Distinct()], cancellationToken).ConfigureAwait(false);
        return OperationResult<CheckoutResultDto>.Ok(new CheckoutResultDto(
            batches.Count, stored.Applied.Count, [.. batches.Select(b => b.Batch.Id)], stale));
    }

    private async Task<CheckoutEntryDto> ProposeAsync(
        CheckoutItem item,
        CheckoutData data,
        DateOnly today,
        CancellationToken cancellationToken)
    {
        // An archived product is not in data.Products: the entry is treated like free text.
        var product = item.ProductId is { } id ? data.Products.GetValueOrDefault(id) : null;
        var location = CheckoutPrefill.ProposeLocation(
            product?.DefaultStorageLocationId,
            product?.LastBatchLocationId,
            product is null ? null : ProductCategories.DefaultStorageType(product.CategoryKey),
            data.Locations);
        var frozenRule = location?.Type == StorageType.Frozen && product is { LastPurchase: null }
            ? await rules.ResolveAsync(product.Id, StorageType.Frozen, cancellationToken).ConfigureAwait(false)
            : ShelfLifeRule.Empty;
        var expiry = CheckoutPrefill.Propose(location?.Type, product?.LastPurchase, frozenRule, today);
        var quantity = item.Quantity ?? new Quantity(1m, product is null ? Unit.Piece : Unit.FromCode(product.DefaultUnitCode));
        return new CheckoutEntryDto(
            item.Id,
            item.Version,
            product?.Id,
            item.Text,
            product?.CategoryKey,
            new QuantityDto(quantity.Value, quantity.Unit.Code),
            location?.Id,
            expiry.ExpiresOn,
            (ExpiryKindDto)expiry.Kind,
            expiry.FromRule,
            item.Quantity is not null);
    }

    private void Report(Guid listId, CheckoutCommitted stored, List<CheckoutBatch> batches, int staleCount)
    {
        var householdId = householdContext.HouseholdId!.Value;
        foreach (var batch in batches)
        {
            InventoryLog.BatchAdded(logger, batch.Batch.Id, batch.ProductId, BatchOrigin.Purchase);
            InventoryMetrics.BatchAdded(BatchOrigin.Purchase);
            if (batch.NewProduct is { } created)
            {
                CatalogLog.ProductCreated(logger, created.Id, householdId);
            }
        }

        CheckoutMetrics.BatchesAdded(batches.Count);
        CheckoutLog.CheckoutCompleted(logger, listId, batches.Count, stored.Applied.Count - batches.Count, staleCount);
    }

    // Hooks run after the commit; a failing one is logged and never fails the checkout (IStockChangedHandler).
    private async Task NotifyStockChangedAsync(IReadOnlyCollection<Guid> productIds, CancellationToken cancellationToken)
    {
        if (productIds.Count == 0)
        {
            return;
        }

        var householdId = householdContext.HouseholdId!.Value;
        foreach (var handler in stockHandlers)
        {
            try
            {
                await handler.OnStockChangedAsync(householdId, productIds, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                CheckoutLog.StockHandlerFailed(logger, exception);
            }
        }
    }
}
