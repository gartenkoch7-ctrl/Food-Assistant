using FoodAssistant.Client.Catalog;
using FoodAssistant.Client.FoodFacts;
using FoodAssistant.Client.Http;
using FoodAssistant.Client.Inventory;
using FoodAssistant.Contracts.Catalog;
using FoodAssistant.Contracts.Common;
using FoodAssistant.Contracts.FoodFacts;
using FoodAssistant.Contracts.Inventory;
using FoodAssistant.UI.Catalog;
using FoodAssistant.UI.Inventory;
using FoodAssistant.UI.Session;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using MudBlazor;

namespace FoodAssistant.UI.Scan;

/// <summary>
/// The steps behind a scan, without any markup so every branch is testable: known product, unknown
/// product with an Open Food Facts suggestion, and "used". Stock is only changed through dialogs the
/// person confirms (add) or the documented rule of <see cref="ScanConsumeRule"/> (used, undoable).
/// </summary>
public sealed class ScanFlow(
    ICatalogClient catalog,
    IInventoryClient inventory,
    IFoodFactsClient foodFacts,
    SessionState session,
    IDialogService dialogs,
    NavigationManager navigation,
    IStringLocalizer<InventoryText> inventoryText,
    IStringLocalizer<CatalogText> catalogText)
{
    private static readonly DialogOptions DialogDefaults = new() { MaxWidth = MaxWidth.Small, FullWidth = true };

    private static readonly ApiError NoLocation = new(409, "scan.no_location", null, new Dictionary<string, string[]>(), null);

    private static readonly ApiError NoHousehold = new(403, "scan.no_household", null, new Dictionary<string, string[]>(), null);

    public async Task<ScanOutcome> HandleAsync(ScanCode code, ScanMode mode, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(code);
        switch (code.Kind)
        {
            case ScanCodeKind.BatchLink:
                navigation.NavigateTo($"/inventory/batches/{code.BatchId:D}");
                return ScanOutcome.Of(ScanOutcomeKind.Navigated);
            case ScanCodeKind.Barcode:
                break;
            default:
                return ScanOutcome.Of(ScanOutcomeKind.UnknownCode);
        }

        if (session.ActiveHouseholdId is not { } household)
        {
            return ScanOutcome.Fail(NoHousehold);
        }

        var found = await catalog.FindByBarcodeAsync(household, code.Text, cancellationToken);
        if (found.IsSuccess)
        {
            return mode == ScanMode.Add
                ? await AddAsync(household, found.Value!, null, BatchOriginDto.Scanned, cancellationToken)
                : await UseAsync(household, found.Value!.Id, found.Value.Name, cancellationToken);
        }

        if (found.Error!.Status != 404)
        {
            return ScanOutcome.Fail(found.Error);
        }

        // Taking stock out of a product that does not exist cannot work; creating one there would be noise.
        return mode == ScanMode.Add
            ? await CreateAndAddAsync(household, code.Text, cancellationToken)
            : ScanOutcome.Of(ScanOutcomeKind.UnknownProduct);
    }

    /// <summary>The manual way: a product picked from the catalog instead of a scanned barcode.</summary>
    public async Task<ScanOutcome> HandleProductAsync(Guid productId, ScanMode mode, CancellationToken cancellationToken)
    {
        if (session.ActiveHouseholdId is not { } household)
        {
            return ScanOutcome.Fail(NoHousehold);
        }

        var found = await catalog.GetProductAsync(household, productId, cancellationToken);
        if (!found.IsSuccess)
        {
            return ScanOutcome.Fail(found.Error!);
        }

        return mode == ScanMode.Add
            ? await AddAsync(household, found.Value!, null, BatchOriginDto.Manual, cancellationToken)
            : await UseAsync(household, productId, found.Value!.Name, cancellationToken);
    }

    /// <summary>Reverts the action of <paramref name="outcome"/>.</summary>
    public async Task<ScanOutcome> UndoAsync(ScanOutcome outcome, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(outcome);
        if (session.ActiveHouseholdId is not { } household || outcome.ActivityId is not { } activity)
        {
            return ScanOutcome.Fail(NoHousehold);
        }

        var undone = await inventory.UndoAsync(household, activity, cancellationToken);
        return undone.IsSuccess ? ScanOutcome.Of(ScanOutcomeKind.Undone, outcome.ProductName) : ScanOutcome.Fail(undone.Error!);
    }

    /// <summary>"Change amount" after a scan: reverts the automatic amount, then asks for the real one.</summary>
    public async Task<ScanOutcome> ChangeAmountAsync(ScanOutcome outcome, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(outcome);
        if (outcome.ProductId is not { } productId)
        {
            return outcome; // Nothing to change: only actions on a product carry its id.
        }

        var undone = await UndoAsync(outcome, cancellationToken);
        if (undone.Kind == ScanOutcomeKind.Failed)
        {
            return undone;
        }

        var (batch, error) = await EarliestBatchAsync(productId, cancellationToken);
        if (batch is null)
        {
            return error is null ? undone : ScanOutcome.Fail(error);
        }

        var parameters = new DialogParameters<ConsumeDialog> { { d => d.Batch, batch }, { d => d.Mode, ConsumptionKindDto.Partial } };
        var dialog = await dialogs.ShowAsync<ConsumeDialog>(inventoryText["PartialTitle"], parameters, DialogDefaults);
        var result = await dialog.Result;
        return result switch
        {
            { Canceled: false, Data: InventoryActionResponse response } =>
                new ScanOutcome(ScanOutcomeKind.Used, outcome.ProductName, productId, response.ActivityId),
            { Canceled: false, Data: ApiError failed } => ScanOutcome.Fail(failed),
            _ => undone, // Closed: the stock stays as before the scan.
        };
    }

    private async Task<ScanOutcome> UseAsync(Guid household, Guid productId, string name, CancellationToken cancellationToken)
    {
        var (batch, error) = await EarliestBatchAsync(productId, cancellationToken);
        if (batch is null)
        {
            return error is null ? ScanOutcome.Of(ScanOutcomeKind.NotInStock, name) : ScanOutcome.Fail(error);
        }

        var used = await inventory.ConsumeProductAsync(household, productId, ScanConsumeRule.For(batch), cancellationToken);
        return used.IsSuccess
            ? new ScanOutcome(ScanOutcomeKind.Used, name, productId, used.Value!.ActivityId)
            : ScanOutcome.Fail(used.Error!);
    }

    // No stock is not an error: a missing or empty product both mean "nothing to use".
    private async Task<(BatchDto? Batch, ApiError? Error)> EarliestBatchAsync(Guid productId, CancellationToken cancellationToken)
    {
        if (session.ActiveHouseholdId is not { } household)
        {
            return (null, NoHousehold);
        }

        var stock = await inventory.GetProductAsync(household, productId, cancellationToken);
        if (!stock.IsSuccess)
        {
            return (null, stock.Error!.Status == 404 ? null : stock.Error);
        }

        return (ScanConsumeRule.Earliest(stock.Value!.Batches), null);
    }

    private async Task<ScanOutcome> CreateAndAddAsync(Guid household, string barcode, CancellationToken cancellationToken)
    {
        var lookup = await foodFacts.LookupAsync(household, barcode, cancellationToken);
        var suggestion = lookup.Value;
        var prefill = ScanPrefill.For(barcode, suggestion);
        var parameters = new DialogParameters<ProductEditDialog>
        {
            { d => d.Prefill, prefill },
            { d => d.Notice, ScanPrefill.Notice(suggestion is not null) },
        };
        var dialog = await dialogs.ShowAsync<ProductEditDialog>(catalogText["ProductDialogNew"], parameters, DialogDefaults);
        var result = await dialog.Result;
        if (result is not { Canceled: false, Data: ProductDto product })
        {
            return ScanOutcome.Of(ScanOutcomeKind.Cancelled);
        }

        var packaged = suggestion?.PackageQuantity is { } quantity && quantity.Unit == product.DefaultUnit ? quantity : null;
        var added = await AddAsync(household, product, packaged, BatchOriginDto.Scanned, cancellationToken);
        return added.Kind == ScanOutcomeKind.Cancelled ? ScanOutcome.Of(ScanOutcomeKind.ProductCreated, product.Name) : added;
    }

    private async Task<ScanOutcome> AddAsync(
        Guid household, ProductDto product, QuantityDto? quantity, BatchOriginDto origin, CancellationToken cancellationToken)
    {
        var locations = await catalog.ListStorageLocationsAsync(household, cancellationToken);
        if (!locations.IsSuccess)
        {
            return ScanOutcome.Fail(locations.Error!);
        }

        if (locations.Value!.Count == 0)
        {
            return ScanOutcome.Fail(NoLocation);
        }

        // The dialog preselects the first place; the product's usual place goes first.
        var ordered = locations.Value.OrderByDescending(l => l.Id == product.DefaultStorageLocationId).ToList();
        var summary = new ProductSummaryDto(product.Id, product.Name, product.Brand, product.CategoryKey, product.DefaultUnit);
        var parameters = new DialogParameters<AddBatchDialog>
        {
            { d => d.Product, summary },
            { d => d.Locations, ordered },
            { d => d.Quantity, quantity ?? new QuantityDto(1m, product.DefaultUnit) },
            { d => d.Barcode, origin == BatchOriginDto.Scanned && product.Barcodes.Count > 0 ? product.Barcodes[0] : null },
            { d => d.Origin, origin },
        };
        var dialog = await dialogs.ShowAsync<AddBatchDialog>(inventoryText["AddTitle"], parameters, DialogDefaults);
        var result = await dialog.Result;
        return result is { Canceled: false, Data: InventoryActionResponse response }
            ? new ScanOutcome(ScanOutcomeKind.Added, product.Name, product.Id, response.ActivityId)
            : ScanOutcome.Of(ScanOutcomeKind.Cancelled);
    }
}
