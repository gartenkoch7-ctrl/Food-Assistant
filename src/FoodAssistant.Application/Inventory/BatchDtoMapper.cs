using FoodAssistant.Application.Households;
using FoodAssistant.Contracts.Catalog;
using FoodAssistant.Contracts.Common;
using FoodAssistant.Contracts.Inventory;
using FoodAssistant.Domain.Expiry;
using FoodAssistant.Domain.Inventory;
using FoodAssistant.Domain.Quantities;

namespace FoodAssistant.Application.Inventory;

/// <summary>
/// Maps batches to the inventory contracts. The status is computed for the household's today and
/// lead time, never stored. Public: the delta feed (track E2) builds the same DTOs.
/// </summary>
public static class BatchDtoMapper
{
    public static BatchDto ToDto(BatchContext context, HouseholdToday today)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(today);
        var batch = context.Batch;
        var evaluation = Evaluate(batch, today);

        // The contract enums mirror the domain enums value by value (ContractEnumTests).
        return new BatchDto(
            batch.Id,
            context.ProductId,
            context.StorageLocationId,
            ToDto(batch.Quantity),
            (StorageTypeDto)batch.StorageType,
            (BatchStateDto)batch.State,
            batch.ExpiresOn,
            (ExpiryKindDto)batch.ExpiryKind,
            batch.PackageExpiresOn,
            (ExpiryKindDto)batch.PackageExpiryKind,
            batch.OpenedOn,
            batch.FrozenOn,
            batch.ThawedOn,
            batch.PreparedOn,
            (BatchOriginDto)context.Origin,
            ToDto(evaluation.Status),
            evaluation.DaysUntilExpiry,
            context.Version);
    }

    /// <summary>
    /// A product with its batches: one total per group of convertible units (in the product's default
    /// unit where it converts), the worst status (expired, soon, ok, no date) and the next expiry.
    /// </summary>
    public static InventoryProductDto ToProductDto(ProductSummaryDto product, IEnumerable<BatchContext> batches, HouseholdToday today)
    {
        ArgumentNullException.ThrowIfNull(product);
        ArgumentNullException.ThrowIfNull(batches);
        ArgumentNullException.ThrowIfNull(today);

        var ordered = batches
            .OrderBy(b => b.Batch.ExpiresOn is null)
            .ThenBy(b => b.Batch.ExpiresOn)
            .ThenBy(b => b.Batch.Id)
            .ToList();
        var worst = ordered.Count == 0
            ? ExpiryStatus.NoDate
            : ordered.Select(b => Evaluate(b.Batch, today).Status).Min();

        return new InventoryProductDto(
            product,
            Totals(ordered.Select(b => b.Batch).ToList(), product.DefaultUnit),
            ToDto(worst),
            ordered.Select(b => b.Batch.ExpiresOn).FirstOrDefault(date => date is not null),
            [.. ordered.Select(b => ToDto(b, today))]);
    }

    /// <summary>Status of a batch for the household's today and lead time.</summary>
    public static ExpiryEvaluation Evaluate(StockBatch batch, HouseholdToday today)
    {
        ArgumentNullException.ThrowIfNull(batch);
        ArgumentNullException.ThrowIfNull(today);
        return ExpiryEvaluator.Evaluate(batch.ExpiresOn, today.Today, today.ExpiryLeadDays);
    }

    public static QuantityDto ToDto(Quantity quantity) => new(quantity.Value, quantity.Unit.Code);

    // The domain order (Expired, Soon, Ok, NoDate) is the severity order; the contract numbers differ.
    public static ExpiryStatusDto ToDto(ExpiryStatus status) => status switch
    {
        ExpiryStatus.Expired => ExpiryStatusDto.Expired,
        ExpiryStatus.Soon => ExpiryStatusDto.Soon,
        ExpiryStatus.Ok => ExpiryStatusDto.Ok,
        _ => ExpiryStatusDto.NoDate,
    };

    private static List<QuantityDto> Totals(List<StockBatch> batches, string defaultUnitCode)
    {
        Unit.TryFromCode(defaultUnitCode, out var defaultUnit);
        var groupUnits = new List<Unit>();
        foreach (var unit in batches.Select(b => b.Quantity.Unit))
        {
            if (groupUnits.Any(group => group.CanConvertTo(unit)))
            {
                continue;
            }

            groupUnits.Add(defaultUnit is not null && unit.CanConvertTo(defaultUnit) && !defaultUnit.IsApproximate
                ? defaultUnit
                : Exact(unit));
        }

        // Three decimals like the stored amounts; sums across units could repeat (1/3 l).
        return
        [
            .. groupUnits.Select(unit => MinimumStockPolicy.TotalStock(batches, unit))
                .Select(total => new QuantityDto(decimal.Round(total.Value, 3), total.Unit.Code)),
        ];
    }

    // Tablespoons and teaspoons only approximate a volume; sum them in an exact unit of the dimension.
    private static Unit Exact(Unit unit) =>
        unit.IsApproximate ? Unit.All.First(u => !u.IsApproximate && u.CanConvertTo(unit)) : unit;
}
