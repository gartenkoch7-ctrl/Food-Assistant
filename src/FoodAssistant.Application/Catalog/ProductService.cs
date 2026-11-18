using System.Diagnostics.Metrics;
using FoodAssistant.Application.Common;
using FoodAssistant.Application.Diagnostics;
using FoodAssistant.Application.Households;
using FoodAssistant.Application.Inventory;
using FoodAssistant.Contracts.Catalog;
using FoodAssistant.Contracts.Common;
using FoodAssistant.Domain.Quantities;
using Microsoft.Extensions.Logging;

namespace FoodAssistant.Application.Catalog;

/// <summary>Products of the household catalog: search, barcode lookup, create, change, archive.</summary>
public sealed class ProductService(
    ICatalogStore store,
    IHouseholdContext household,
    IEnumerable<IStockChangedHandler> stockHandlers,
    TimeProvider time,
    ILogger<ProductService> logger)
{
    private const int DefaultLimit = 50;
    private const int MaxLimit = 200;

    private static readonly Counter<long> ProductsCreated =
        Telemetry.Meter.CreateCounter<long>("foodassistant.catalog.products.created", "{product}");

    public async Task<OperationResult<CursorPage<ProductSummaryDto>>> SearchAsync(
        Guid householdId,
        string? query,
        string? cursor,
        int? limit,
        CancellationToken cancellationToken)
    {
        household.EnsureActive(householdId);

        var text = string.IsNullOrWhiteSpace(query) ? null : query.Trim();
        if (text is { Length: > CatalogValidation.QueryMaxLength })
        {
            return OperationResult<CursorPage<ProductSummaryDto>>.Invalid(
                new Dictionary<string, string[]> { ["query"] = ["too_long"] }, CatalogErrorCodes.ValidationFailed);
        }

        ProductCursor? after = null;
        if (!string.IsNullOrEmpty(cursor) && !ProductCursor.TryDecode(cursor, out after))
        {
            return OperationResult<CursorPage<ProductSummaryDto>>.Invalid(
                new Dictionary<string, string[]> { ["cursor"] = ["invalid"] }, ErrorCodes.CursorInvalid);
        }

        var take = limit is null or < 1 ? DefaultLimit : Math.Min(limit.Value, MaxLimit);

        // One more than the page size tells whether another page exists.
        var found = await store.SearchAsync(text, after, take + 1, cancellationToken).ConfigureAwait(false);
        if (found.Count <= take)
        {
            return OperationResult<CursorPage<ProductSummaryDto>>.Ok(new CursorPage<ProductSummaryDto>(found, null));
        }

        var items = found.Take(take).ToList();
        var next = new ProductCursor(items[^1].Name, items[^1].Id).Encode();
        return OperationResult<CursorPage<ProductSummaryDto>>.Ok(new CursorPage<ProductSummaryDto>(items, next));
    }

    public async Task<OperationResult<ProductDto>> GetAsync(Guid householdId, Guid productId, CancellationToken cancellationToken)
    {
        household.EnsureActive(householdId);
        var product = await store.GetProductAsync(productId, cancellationToken).ConfigureAwait(false);
        return product is null
            ? OperationResult<ProductDto>.NotFound(CatalogErrorCodes.ProductNotFound)
            : OperationResult<ProductDto>.Ok(product);
    }

    public async Task<OperationResult<ProductDto>> FindByBarcodeAsync(Guid householdId, string code, CancellationToken cancellationToken)
    {
        household.EnsureActive(householdId);
        if (!CatalogValidation.IsBarcode(code))
        {
            return OperationResult<ProductDto>.Invalid(
                new Dictionary<string, string[]> { ["barcode"] = ["invalid"] }, CatalogErrorCodes.ValidationFailed);
        }

        var product = await store.FindByBarcodeAsync(code, cancellationToken).ConfigureAwait(false);
        return product is null
            ? OperationResult<ProductDto>.NotFound(CatalogErrorCodes.ProductNotFound)
            : OperationResult<ProductDto>.Ok(product);
    }

    public async Task<OperationResult<ProductDto>> CreateAsync(
        Guid householdId,
        CreateProductRequest request,
        CancellationToken cancellationToken)
    {
        household.EnsureActive(householdId);
        ArgumentNullException.ThrowIfNull(request);
        using var activity = Telemetry.Source.StartActivity("foodassistant.catalog.create_product");

        var errors = CatalogValidation.ValidateProduct(
            request.Name,
            request.Brand,
            request.CategoryKey,
            request.DefaultUnit,
            request.SectionKey,
            request.MinimumStock,
            request.Barcodes,
            request.FoodFactsBarcode);
        var errorsWithId = request.Id == Guid.Empty
            ? new Dictionary<string, string[]>(errors, StringComparer.Ordinal) { ["id"] = ["invalid"] }
            : errors;
        if (errorsWithId.Count > 0)
        {
            return Finish(activity, OperationResult<ProductDto>.Invalid(errorsWithId, CatalogErrorCodes.ValidationFailed));
        }

        var product = new NewProduct(
            request.Id ?? Guid.CreateVersion7(),
            householdId,
            request.Name.Trim(),
            Normalize(request.Brand),
            request.CategoryKey,
            Unit.FromCode(request.DefaultUnit),
            request.DefaultStorageLocationId,
            request.SectionKey,
            ToQuantity(request.MinimumStock),
            Distinct(request.Barcodes),
            request.FoodFactsBarcode,
            time.GetUtcNow());

        var result = await store.AddProductAsync(product, cancellationToken).ConfigureAwait(false);
        if (result.Status == OperationStatus.Created)
        {
            CatalogLog.ProductCreated(logger, product.Id, householdId);
            ProductsCreated.Add(1, new KeyValuePair<string, object?>("source", product.FoodFactsBarcode is null ? "manual" : "food_facts"));
            if (product.MinimumStock is not null)
            {
                await NotifyHandlersAsync(householdId, product.Id, cancellationToken).ConfigureAwait(false);
            }
        }

        return Finish(activity, result);
    }

    /// <param name="expectedVersion">From <c>If-Match</c>; <see langword="null"/> means any version.</param>
    public async Task<OperationResult<ProductDto>> UpdateAsync(
        Guid householdId,
        Guid productId,
        long? expectedVersion,
        UpdateProductRequest request,
        CancellationToken cancellationToken)
    {
        household.EnsureActive(householdId);
        ArgumentNullException.ThrowIfNull(request);
        using var activity = Telemetry.Source.StartActivity("foodassistant.catalog.update_product");

        var errors = CatalogValidation.ValidateProduct(
            request.Name,
            request.Brand,
            request.CategoryKey,
            request.DefaultUnit,
            request.SectionKey,
            request.MinimumStock,
            request.Barcodes);
        if (errors.Count > 0)
        {
            return Finish(activity, OperationResult<ProductDto>.Invalid(errors, CatalogErrorCodes.ValidationFailed));
        }

        var before = await store.GetProductAsync(productId, cancellationToken).ConfigureAwait(false);
        if (before is null)
        {
            return Finish(activity, OperationResult<ProductDto>.NotFound(CatalogErrorCodes.ProductNotFound));
        }

        // A PUT replaces the product: omitted barcodes mean none.
        var changes = new ProductChanges(
            request.Name.Trim(),
            Normalize(request.Brand),
            request.CategoryKey,
            Unit.FromCode(request.DefaultUnit),
            request.DefaultStorageLocationId,
            request.SectionKey,
            ToQuantity(request.MinimumStock),
            Distinct(request.Barcodes),
            time.GetUtcNow());

        var result = await store.UpdateProductAsync(productId, expectedVersion, changes, cancellationToken).ConfigureAwait(false);
        if (result.Status == OperationStatus.Ok && !Equals(before.MinimumStock, request.MinimumStock))
        {
            await NotifyHandlersAsync(householdId, productId, cancellationToken).ConfigureAwait(false);
        }

        return Finish(activity, result);
    }

    public async Task<OperationResult<ProductDto>> ArchiveAsync(
        Guid householdId,
        Guid productId,
        long? expectedVersion,
        CancellationToken cancellationToken)
    {
        household.EnsureActive(householdId);
        using var activity = Telemetry.Source.StartActivity("foodassistant.catalog.archive_product");

        var result = await store.ArchiveProductAsync(productId, expectedVersion, time.GetUtcNow(), cancellationToken).ConfigureAwait(false);
        if (result.Status == OperationStatus.Deleted)
        {
            CatalogLog.ProductArchived(logger, productId, householdId);
        }

        return Finish(activity, result);
    }

    private static OperationResult<ProductDto> Finish(System.Diagnostics.Activity? activity, OperationResult<ProductDto> result)
    {
        activity?.SetTag("foodassistant.outcome", result.Status.ToString());
        return result;
    }

    private static string? Normalize(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    private static List<string> Distinct(IReadOnlyList<string>? barcodes) =>
        barcodes?.Distinct(StringComparer.Ordinal).ToList() ?? [];

    private static Quantity? ToQuantity(QuantityDto? dto) =>
        dto is null ? null : new Quantity(dto.Value, Unit.FromCode(dto.Unit));

    // The minimum stock may now be reached or no longer reached; the restock track reacts.
    // A failing handler never fails the change, which is already saved.
    private async Task NotifyHandlersAsync(Guid householdId, Guid productId, CancellationToken cancellationToken)
    {
        foreach (var handler in stockHandlers)
        {
            try
            {
                await handler.OnStockChangedAsync(householdId, [productId], cancellationToken).ConfigureAwait(false);
            }
#pragma warning disable CA1031 // See above: handlers are independent of each other and of the caller.
            catch (Exception exception) when (exception is not OperationCanceledException)
#pragma warning restore CA1031
            {
                CatalogLog.StockHandlerFailed(logger, productId, exception);
            }
        }
    }
}
