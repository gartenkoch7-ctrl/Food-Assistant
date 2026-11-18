using Bunit;
using FoodAssistant.Contracts.Catalog;
using FoodAssistant.Contracts.Common;
using FoodAssistant.Contracts.FoodFacts;
using FoodAssistant.Contracts.Inventory;
using FoodAssistant.UI.Inventory;
using FoodAssistant.UI.Tests.Support;
using FoodAssistant.UI.Tests.Inventory;
using FoodAssistant.UI.Scan;
using Microsoft.Extensions.DependencyInjection;

namespace FoodAssistant.UI.Tests.Scan;

/// <summary>Shared set-up of the scan flow tests: flow under test, barcode constants and dialog helpers.</summary>
public abstract class ScanFlowTestContext : InventoryTestContext
{
    protected const string Barcode = "4006381333931";

    protected ScanFlow Flow => Services.GetRequiredService<ScanFlow>();

    protected static CancellationToken Ct => Xunit.TestContext.Current.CancellationToken;

    protected ProductDto KnownProduct(string unit = "l", Guid? defaultLocation = null)
    {
        var product = Product("Milch", "dairy", null, 1, Barcode) with { DefaultUnit = unit, DefaultStorageLocationId = defaultLocation };
        Catalog.Products.Add(product);
        return product;
    }

    protected static FoodFactsProductDto Suggestion(QuantityDto? package = null) =>
        new(Barcode, "Hafermilch", "Oatly", "1 l", package ?? new QuantityDto(1m, "l"), "dairy", false);

    /// <summary>Starts a scan on the renderer thread; dialogs it opens are driven by the test, then the task is awaited.</summary>
    protected Task<ScanOutcome> Start(ScanCode code, ScanMode mode) =>
        Dialogs.InvokeAsync(() => Flow.HandleAsync(code, mode, Ct));

    protected void WaitForDialogText(string text) =>
        Dialogs.WaitForAssertion(() => Assert.Contains(text, Dialogs.Markup, StringComparison.Ordinal));

    protected void Click(string buttonText) => ButtonByText(Dialogs, buttonText).Click();

    protected AddBatchRequest SingleAddRequest() => Assert.IsType<AddBatchRequest>(Assert.Single(Inventory.Requests));

    protected BatchDto PieceBatch(ProductDto product, decimal amount, int days = 2, string unit = "piece") =>
        Batch(new ProductSummaryDto(product.Id, product.Name, null, product.CategoryKey, product.DefaultUnit), days, ExpiryStatusDto.Soon, amount)
            with { Quantity = new QuantityDto(amount, unit) };

    protected InventoryProductDto StockOf(ProductDto product, params BatchDto[] batches) =>
        Stock(new ProductSummaryDto(product.Id, product.Name, null, product.CategoryKey, product.DefaultUnit), batches);
}
