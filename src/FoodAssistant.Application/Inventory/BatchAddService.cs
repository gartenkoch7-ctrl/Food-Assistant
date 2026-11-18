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

/// <summary>New batches: bought or scanned goods, and leftovers (docs/kb/vorrat-und-haltbarkeit.md).</summary>
public sealed class BatchAddService(
    IInventoryStore store,
    IShelfLifeRules rules,
    IHouseholdCalendar calendar,
    IHouseholdContext householdContext,
    InventoryCommitter committer,
    ILogger<BatchAddService> logger)
{
    /// <summary>
    /// Adds a batch with the storage type of its location. In a freezer (F5): with package date and
    /// not <see cref="AddBatchRequest.SelfFrozen"/> it is bought frozen food (sealed, package date
    /// applies); otherwise it is frozen at home today (rule "frozen", the package date rests).
    /// </summary>
    public async Task<OperationResult<InventoryActionResponse>> AddAsync(
        Guid householdId,
        AddBatchRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        householdContext.EnsureActive(householdId);
        using var activity = Telemetry.Source.StartActivity("foodassistant.inventory.add_batch");

        var errors = InventoryValidation.NewErrors();
        InventoryValidation.Id(request.Id, errors);
        var quantity = InventoryValidation.Quantity(request.Quantity, "quantity", errors);
        var kind = InventoryValidation.PackageDate(request.PackageExpiresOn, request.PackageExpiryKind, errors);

        // Leftovers get their date from a rule and go through CreateLeftoverAsync.
        if (InventoryValidation.Defined(request.Origin, "origin", errors) && request.Origin == BatchOriginDto.Leftover)
        {
            errors["origin"] = ["invalid"];
        }

        if (errors.Count > 0 || quantity is not { } amount || kind is not { } expiryKind)
        {
            return Finish(activity, OperationResult<InventoryActionResponse>.Invalid(errors, InventoryErrorCodes.ValidationFailed));
        }

        var (location, failure) = await LoadPlacementAsync(request.ProductId, request.StorageLocationId, cancellationToken).ConfigureAwait(false);
        if (location is null)
        {
            return Finish(activity, failure!);
        }

        var today = await calendar.GetTodayAsync(cancellationToken).ConfigureAwait(false);
        var id = request.Id ?? Guid.CreateVersion7();
        StockBatch batch;
        if (location.Type == StorageType.Frozen && (request.PackageExpiresOn is null || request.SelfFrozen))
        {
            var rule = await rules.ResolveAsync(request.ProductId, StorageType.Frozen, cancellationToken).ConfigureAwait(false);
            batch = new StockBatch(id, amount, StorageType.Chilled, request.PackageExpiresOn, expiryKind);
            batch.Freeze(today.Today, rule);
        }
        else
        {
            batch = new StockBatch(id, amount, location.Type, request.PackageExpiresOn, expiryKind);
        }

        var origin = (BatchOrigin)request.Origin;
        var result = await CommitAsync(
            InventoryActivityKinds.BatchAdded, BatchStep.Created(batch, request.ProductId, location.Id, origin, today.Today), today, cancellationToken)
            .ConfigureAwait(false);
        return Finish(activity, result);
    }

    /// <summary>Records a leftover; its date comes from the rule "as leftover" for the location's type.</summary>
    public async Task<OperationResult<InventoryActionResponse>> CreateLeftoverAsync(
        Guid householdId,
        CreateLeftoverRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        householdContext.EnsureActive(householdId);
        using var activity = Telemetry.Source.StartActivity("foodassistant.inventory.create_leftover");

        var errors = InventoryValidation.NewErrors();
        InventoryValidation.Id(request.Id, errors);
        if (InventoryValidation.Quantity(request.Quantity, "quantity", errors) is not { } amount || errors.Count > 0)
        {
            return Finish(activity, OperationResult<InventoryActionResponse>.Invalid(errors, InventoryErrorCodes.ValidationFailed));
        }

        var (location, failure) = await LoadPlacementAsync(request.ProductId, request.StorageLocationId, cancellationToken).ConfigureAwait(false);
        if (location is null)
        {
            return Finish(activity, failure!);
        }

        var rule = await rules.ResolveAsync(request.ProductId, location.Type, cancellationToken).ConfigureAwait(false);
        var today = await calendar.GetTodayAsync(cancellationToken).ConfigureAwait(false);
        var batch = StockBatch.CreateLeftover(request.Id ?? Guid.CreateVersion7(), amount, location.Type, today.Today, rule);
        var result = await CommitAsync(
            InventoryActivityKinds.LeftoverCreated,
            BatchStep.Created(batch, request.ProductId, location.Id, BatchOrigin.Leftover, today.Today),
            today,
            cancellationToken).ConfigureAwait(false);
        return Finish(activity, result);
    }

    private async Task<(StorageLocationInfo? Location, OperationResult<InventoryActionResponse>? Failure)> LoadPlacementAsync(
        Guid productId,
        Guid locationId,
        CancellationToken cancellationToken)
    {
        if (await store.GetProductAsync(productId, cancellationToken).ConfigureAwait(false) is null)
        {
            return (null, OperationResult<InventoryActionResponse>.NotFound(CatalogErrorCodes.ProductNotFound));
        }

        var location = await store.GetLocationAsync(locationId, cancellationToken).ConfigureAwait(false);
        return location is null
            ? (null, OperationResult<InventoryActionResponse>.NotFound(CatalogErrorCodes.LocationNotFound))
            : (location, null);
    }

    private async Task<OperationResult<InventoryActionResponse>> CommitAsync(
        string kind,
        BatchStep step,
        HouseholdToday today,
        CancellationToken cancellationToken)
    {
        var result = await committer.ApplyAsync(
            new InventoryAction(kind, step.ProductId, [step], StockChanged: true), today, created: true, cancellationToken)
            .ConfigureAwait(false);
        if (result.Succeeded)
        {
            InventoryLog.BatchAdded(logger, step.Batch.Id, step.ProductId, step.Origin);
            InventoryMetrics.BatchAdded(step.Origin);
        }

        return result;
    }
}
