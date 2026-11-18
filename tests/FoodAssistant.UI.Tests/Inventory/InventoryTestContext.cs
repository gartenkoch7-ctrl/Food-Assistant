using FoodAssistant.Contracts.Catalog;
using FoodAssistant.Contracts.Common;
using FoodAssistant.Contracts.Households;
using FoodAssistant.Contracts.Inventory;
using FoodAssistant.UI.Tests.Catalog;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;

namespace FoodAssistant.UI.Tests.Inventory;

/// <summary>Inventory test data: a fridge, a freezer and builders for batches and products.</summary>
public abstract class InventoryTestContext : CatalogTestContext
{
    protected InventoryTestContext(string culture = "de", HouseholdRole role = HouseholdRole.Member)
        : base(culture, role)
    {
        Fridge = Location("Kühlschrank");
        Freezer = Location("Gefrierschrank", StorageTypeDto.Frozen);
        Catalog.Locations.AddRange([Fridge, Freezer]);
        Snackbars = Render<MudSnackbarProvider>();
    }

    protected StorageLocationDto Fridge { get; }

    protected StorageLocationDto Freezer { get; }

    protected IReadOnlyList<StorageLocationDto> Locations => [Fridge, Freezer];

    protected Bunit.IRenderedComponent<MudSnackbarProvider> Snackbars { get; }

    protected ISnackbar SnackbarService => Services.GetRequiredService<ISnackbar>();

    protected static ProductSummaryDto Summary(string name, string category = "dairy") =>
        new(Guid.CreateVersion7(), name, null, category, "l");

    protected BatchDto Batch(
        ProductSummaryDto product,
        int? days,
        ExpiryStatusDto status,
        decimal amount = 1m,
        ExpiryKindDto kind = ExpiryKindDto.BestBefore,
        BatchStateDto state = BatchStateDto.Sealed,
        StorageLocationDto? location = null)
    {
        ArgumentNullException.ThrowIfNull(product);
        var place = location ?? Fridge;
        DateOnly? date = days is { } d ? new DateOnly(2026, 11, 18).AddDays(d) : null;
        return new BatchDto(
            Guid.CreateVersion7(), product.Id, place.Id, new QuantityDto(amount, "l"), place.Type, state,
            date, date is null ? ExpiryKindDto.None : kind, date, date is null ? ExpiryKindDto.None : kind,
            null, null, null, null, BatchOriginDto.Manual, date is null ? ExpiryStatusDto.NoDate : status, days, 7);
    }

    protected InventoryProductDto Stock(ProductSummaryDto product, params BatchDto[] batches)
    {
        var stock = new InventoryProductDto(
            product,
            [new QuantityDto(batches.Sum(b => b.Quantity.Value), "l")],
            batches.Length == 0 ? ExpiryStatusDto.NoDate : batches[0].Status,
            batches.Select(b => b.ExpiresOn).FirstOrDefault(d => d is not null),
            batches);
        Inventory.Products.Add(stock);
        return stock;
    }
}
