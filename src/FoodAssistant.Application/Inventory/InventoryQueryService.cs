using FoodAssistant.Application.Common;
using FoodAssistant.Application.Households;
using FoodAssistant.Contracts.Catalog;
using FoodAssistant.Contracts.Inventory;
using FoodAssistant.Domain.Expiry;

namespace FoodAssistant.Application.Inventory;

/// <summary>Reading the inventory: overview, one product with its batches, one batch.</summary>
public sealed class InventoryQueryService(
    IInventoryStore store,
    IHouseholdCalendar calendar,
    IHouseholdContext householdContext)
{
    /// <summary>
    /// Products with stock, sorted by next expiry (none last), name and id. Status and location
    /// filter the batches; a product without matching batches is left out, and its totals and
    /// status cover only the matching ones.
    /// </summary>
    /// <remarks>
    /// Filters and sorts in memory over all batches of the household: a household has at most a few
    /// thousand batches, and the status depends on the household's today. Move to SQL if that changes.
    /// </remarks>
    /// <returns>Up to <see cref="InventoryQuery.Take"/> products after <see cref="InventoryQuery.After"/>.</returns>
    public async Task<OperationResult<IReadOnlyList<InventoryProductDto>>> ListAsync(
        Guid householdId,
        InventoryQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        householdContext.EnsureActive(householdId);

        var errors = InventoryValidation.NewErrors();
        ExpiryStatus? status = query.Status switch
        {
            null or "all" => null,
            "expired" => ExpiryStatus.Expired,
            "soon" => ExpiryStatus.Soon,
            _ => Error(errors, "status", "invalid"),
        };
        var name = query.Query?.Trim();
        if (name?.Length > InventoryValidation.QueryMaxLength)
        {
            errors["query"] = ["too_long"];
        }

        if (errors.Count > 0)
        {
            return OperationResult<IReadOnlyList<InventoryProductDto>>.Invalid(errors, InventoryErrorCodes.ValidationFailed);
        }

        var today = await calendar.GetTodayAsync(cancellationToken).ConfigureAwait(false);
        var products = await store.ListAsync(cancellationToken).ConfigureAwait(false);
        var page = products
            .Where(p => string.IsNullOrEmpty(name) || p.Product.Name.Contains(name, StringComparison.OrdinalIgnoreCase))
            .Select(p => BatchDtoMapper.ToProductDto(
                p.Product,
                p.Batches.Where(b => (query.LocationId is not { } location || b.StorageLocationId == location)
                    && (status is not { } wanted || BatchDtoMapper.Evaluate(b.Batch, today).Status == wanted)),
                today))
            .Where(p => p.Batches.Count > 0)
            .Select(p => (Position: PositionOf(p), Product: p))
            .Where(p => query.After is null || Compare(p.Position, query.After) > 0)
            .OrderBy(p => p.Position, Comparer<InventoryCursor>.Create(Compare))
            .Take(query.Take)
            .Select(p => p.Product)
            .ToList();
        return OperationResult<IReadOnlyList<InventoryProductDto>>.Ok(page);
    }

    /// <summary>One product with all its batches (also without stock).</summary>
    public async Task<OperationResult<InventoryProductDto>> GetProductAsync(
        Guid householdId,
        Guid productId,
        CancellationToken cancellationToken)
    {
        householdContext.EnsureActive(householdId);
        var product = await store.GetProductAsync(productId, cancellationToken).ConfigureAwait(false);
        if (product is null)
        {
            return OperationResult<InventoryProductDto>.NotFound(CatalogErrorCodes.ProductNotFound);
        }

        var batches = await store.GetBatchesOfProductAsync(productId, cancellationToken).ConfigureAwait(false);
        var today = await calendar.GetTodayAsync(cancellationToken).ConfigureAwait(false);
        return OperationResult<InventoryProductDto>.Ok(BatchDtoMapper.ToProductDto(product, batches, today));
    }

    public async Task<OperationResult<BatchDto>> GetBatchAsync(Guid householdId, Guid batchId, CancellationToken cancellationToken)
    {
        householdContext.EnsureActive(householdId);
        var batch = await store.GetBatchAsync(batchId, cancellationToken).ConfigureAwait(false);
        if (batch is null)
        {
            return OperationResult<BatchDto>.NotFound(InventoryErrorCodes.BatchNotFound);
        }

        var today = await calendar.GetTodayAsync(cancellationToken).ConfigureAwait(false);
        return OperationResult<BatchDto>.Ok(BatchDtoMapper.ToDto(batch, today));
    }

    /// <summary>Sort position of a product in the overview (the cursor of the next page).</summary>
    public static InventoryCursor PositionOf(InventoryProductDto product)
    {
        ArgumentNullException.ThrowIfNull(product);
        return new InventoryCursor(product.NextExpiry, product.Product.Name, product.Product.Id);
    }

    private static int Compare(InventoryCursor? left, InventoryCursor? right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);
        var result = (left.NextExpiry is null).CompareTo(right.NextExpiry is null);
        if (result == 0)
        {
            result = Nullable.Compare(left.NextExpiry, right.NextExpiry);
        }

        if (result == 0)
        {
            result = string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase);
        }

        if (result == 0)
        {
            result = string.CompareOrdinal(left.Name, right.Name);
        }

        return result != 0 ? result : left.ProductId.CompareTo(right.ProductId);
    }

    private static ExpiryStatus? Error(Dictionary<string, string[]> errors, string field, string code)
    {
        errors[field] = [code];
        return null;
    }
}
