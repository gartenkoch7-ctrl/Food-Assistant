using Bunit;
using FoodAssistant.Client.Http;
using FoodAssistant.Contracts.Catalog;
using FoodAssistant.UI.Catalog;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace FoodAssistant.UI.Tests.Catalog;

public sealed class ProductEditDialogTests : CatalogTestContext
{
    private static CreateProductRequest Prefill(string name = "Haferdrink", string? foodFacts = null) =>
        new(null, name, null, "dairy", "l", null, null, null, null, foodFacts);

    private Task<IDialogReference> OpenAsync(ProductDto? product = null, CreateProductRequest? prefill = null, RenderFragment? notice = null) =>
        ShowAsync<ProductEditDialog>(p =>
        {
            p.Add(d => d.Product, product);
            p.Add(d => d.Prefill, prefill);
            p.Add(d => d.Notice, notice);
        });

    private static ApiError Error(int status, string code, params (string Field, string Code)[] fields) =>
        new(status, code, null, fields.ToDictionary(f => f.Field, f => new[] { f.Code }), null);

    [Fact]
    public async Task NewProduct_HasNoArchiveButton_AndShowsTheNotice()
    {
        await OpenAsync(notice: b => b.AddMarkupContent(0, "<p>Quelle: Open Food Facts</p>"));

        Assert.Contains("Produkt anlegen", Dialogs.Markup, StringComparison.Ordinal);
        Assert.Contains("Quelle: Open Food Facts", Dialogs.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain(Dialogs.FindAll("button"), b => b.TextContent.Trim() == "Archivieren");
        Assert.DoesNotContain("Haltbarkeit anpassen", Dialogs.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExistingProduct_LinksToItsShelfLifePage()
    {
        var milk = Product("Vollmilch");
        await OpenAsync(milk);

        var link = Dialogs.FindAll("a").First(a => a.TextContent.Contains("Haltbarkeit anpassen", StringComparison.Ordinal));

        Assert.Equal($"/products/{milk.Id:D}/shelf-life", link.GetAttribute("href"));
    }

    [Fact]
    public async Task Prefill_FillsTheFields_AndSavingCreatesWithTheFoodFactsBarcode()
    {
        var reference = await OpenAsync(prefill: Prefill(foodFacts: "4006381333931"));

        Assert.Equal("Haferdrink", InputFor(Dialogs, "Name").GetAttribute("value"));
        ButtonByText(Dialogs, "Speichern").Click();

        var result = await reference.Result;
        var created = Assert.IsType<ProductDto>(result!.Data);
        Assert.Equal("Haferdrink", created.Name);
        Assert.True(created.FromOpenFoodFacts);
        Assert.Contains("create-product", Catalog.Calls);
    }

    [Fact]
    public async Task EditingAProduct_SendsTheEtagAndClosesWithTheUpdatedProduct()
    {
        var milk = Product("Vollmilch", version: 4);
        Catalog.Products.Add(milk);
        var reference = await OpenAsync(milk);

        InputFor(Dialogs, "Name").Change("Bio-Vollmilch");
        ButtonByText(Dialogs, "Speichern").Click();

        var updated = Assert.IsType<ProductDto>((await reference.Result)!.Data);
        Assert.Equal(("Bio-Vollmilch", 5), (updated.Name, (int)updated.Version));
    }

    [Fact]
    public async Task FieldErrors_AppearAtTheField_AndTheDialogStaysOpen()
    {
        var milk = Product("Vollmilch");
        Catalog.Products.Add(milk);
        var reference = await OpenAsync(milk);
        Catalog.FailWith = Error(400, "catalog.validation_failed", ("name", "required"));

        ButtonByText(Dialogs, "Speichern").Click();

        Assert.Contains("Bitte ausfüllen.", Dialogs.Markup, StringComparison.Ordinal);
        Assert.Contains("Bitte prüfe die markierten Felder.", Dialogs.Find("[role=alert]").TextContent, StringComparison.Ordinal);
        Assert.False(reference.Result.IsCompleted);
    }

    [Fact]
    public async Task BarcodeInUse_ShowsTheTranslatedError()
    {
        var milk = Product("Vollmilch");
        Catalog.Products.Add(milk);
        await OpenAsync(milk);
        Catalog.FailWith = Error(409, "catalog.barcode_in_use");

        ButtonByText(Dialogs, "Speichern").Click();

        Assert.Contains("gehört schon zu einem anderen Produkt", Dialogs.Find("[role=alert]").TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OutdatedVersion_ShowsTheNoticeAndReloadsTheStoredProduct()
    {
        var milk = Product("Vollmilch", version: 1);
        Catalog.Products.Add(milk);
        var reference = await OpenAsync(milk);
        Catalog.Products[0] = milk with { Name = "Von jemand anderem", Version = 2 };

        InputFor(Dialogs, "Name").Change("Meine Änderung");
        ButtonByText(Dialogs, "Speichern").Click();

        Assert.Contains("inzwischen geändert und neu geladen", Dialogs.Markup, StringComparison.Ordinal);
        Assert.Equal("Von jemand anderem", InputFor(Dialogs, "Name").GetAttribute("value"));
        Assert.False(reference.Result.IsCompleted);

        // The second save uses the reloaded version and succeeds.
        ButtonByText(Dialogs, "Speichern").Click();
        Assert.Equal(3, (int)Assert.IsType<ProductDto>((await reference.Result)!.Data).Version);
    }

    [Fact]
    public async Task Archive_AsksForConfirmation_ThenArchives()
    {
        var milk = Product("Vollmilch");
        Catalog.Products.Add(milk);
        var reference = await OpenAsync(milk);

        ButtonByText(Dialogs, "Archivieren").Click();
        Dialogs.WaitForAssertion(() => Assert.Contains("Produkt archivieren?", Dialogs.Markup, StringComparison.Ordinal));
        Dialogs.Find(".mud-message-box__yes-button").Click();

        Assert.False((await reference.Result)!.Canceled);
        Assert.Empty(Catalog.Products);
    }

    [Fact]
    public async Task Archive_WithStock_ShowsTheReason()
    {
        var milk = Product("Vollmilch");
        Catalog.Products.Add(milk);
        await OpenAsync(milk);
        Catalog.FailWith = Error(409, "catalog.product_has_stock");

        ButtonByText(Dialogs, "Archivieren").Click();
        Dialogs.WaitForAssertion(() => Dialogs.Find(".mud-message-box__yes-button"));
        Dialogs.Find(".mud-message-box__yes-button").Click();

        Dialogs.WaitForAssertion(() => Assert.Contains("noch Bestand im Vorrat", Dialogs.Markup, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Barcodes_CanBeAddedAndRemoved_InvalidOnesAreRefused()
    {
        var milk = Product("Vollmilch", barcodes: "4006381333931");
        Catalog.Products.Add(milk);
        var reference = await OpenAsync(milk);

        InputFor(Dialogs, "Barcode hinzufügen").Input("123");
        ButtonByText(Dialogs, "Hinzufügen").Click();
        Assert.Contains("8, 12, 13 oder 14 Ziffern.", Dialogs.Markup, StringComparison.Ordinal);

        InputFor(Dialogs, "Barcode hinzufügen").Input("12345678");
        ButtonByText(Dialogs, "Hinzufügen").Click();
        Dialogs.Find("button[aria-label='Barcode 4006381333931 entfernen']").Click();
        ButtonByText(Dialogs, "Speichern").Click();

        var saved = Assert.IsType<ProductDto>((await reference.Result)!.Data);
        Assert.Equal(["12345678"], saved.Barcodes);
    }

    [Fact]
    public async Task English_ShowsEnglishTexts()
    {
        UseCulture("en");

        await OpenAsync(Product("Milk"));

        Assert.Contains("Edit product", Dialogs.Markup, StringComparison.Ordinal);
        Assert.Contains("Default unit", Dialogs.Markup, StringComparison.Ordinal);
        Assert.Contains("Adjust shelf life", Dialogs.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EveryFieldHasALabel()
    {
        await OpenAsync(Product("Milk"));

        foreach (var label in new[] { "Name", "Marke", "Kategorie", "Standardeinheit", "Standard-Lagerort", "Bereich im Laden", "Mindestbestand", "Barcode hinzufügen" })
        {
            Assert.Contains(Dialogs.FindAll("label"), l => l.TextContent.Contains(label, StringComparison.Ordinal));
        }
    }
}
