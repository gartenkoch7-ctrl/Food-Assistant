using Bunit;
using FoodAssistant.Client.ShelfLife;
using FoodAssistant.Contracts.Catalog;
using FoodAssistant.Contracts.Inventory;
using FoodAssistant.Contracts.ShelfLife;
using FoodAssistant.UI.ShelfLife;
using FoodAssistant.UI.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;

namespace FoodAssistant.UI.Tests.ShelfLife;

public sealed class ProductShelfLifePageTests : UiTestContext
{
    private static readonly Guid Milk = Guid.Parse("018f0000-0000-7000-8000-0000000000b1");

    private readonly FakeShelfLifeClient _client = new();

    public ProductShelfLifePageTests()
    {
        Services.AddSingleton<IShelfLifeClient>(_client);
        _client.Products[Milk] = new ProductShelfLifeDto(Milk, "dairy",
        [
            FakeShelfLifeClient.Row("dairy", StorageTypeDto.Chilled, new ShelfLifeDaysDto(3, null, null, null), new ShelfLifeDaysDto(null, null, null, 1)),
            FakeShelfLifeClient.Row("dairy", StorageTypeDto.Frozen),
            FakeShelfLifeClient.Row("dairy", StorageTypeDto.Ambient),
        ]);
    }

    private IRenderedComponent<ProductShelfLifePage> RenderPage(Guid productId) =>
        Render<ProductShelfLifePage>(p => p.Add(x => x.ProductId, productId));

    [Fact]
    public void Page_ShowsTheProductRowsWithCategoryAndOwnOrigin()
    {
        Catalog.Products.Add(Product());

        var cut = RenderPage(Milk);

        Assert.Equal("Haltbarkeit: Vollmilch", cut.Find("h1").TextContent);
        Assert.Contains("Kategorie: Milchprodukte.", cut.Markup, StringComparison.Ordinal);
        Assert.Equal(["Gekühlt", "Gefroren", "Raumtemperatur"], cut.FindAll("h3").Select(h => h.TextContent));
        var chilled = cut.FindAll(".fa-shelf-life-row")[0].TextContent;
        Assert.Contains("Kategorie", chilled, StringComparison.Ordinal);
        Assert.Contains("Eigen", chilled, StringComparison.Ordinal);
        Assert.Equal("/settings/shelf-life", cut.Find(".fa-shelf-life-back a").GetAttribute("href"));
    }

    [Fact]
    public void WithoutCatalogName_UsesAGenericTitle()
    {
        var cut = RenderPage(Milk);

        Assert.Equal("Haltbarkeit des Produkts", cut.Find("h1").TextContent);
    }

    [Fact]
    public void UnknownProduct_ShowsNotFound()
    {
        var cut = RenderPage(Guid.CreateVersion7());

        Assert.Contains("Das Produkt wurde nicht gefunden.", cut.Find(".fa-api-error").TextContent, StringComparison.Ordinal);
        Assert.Empty(cut.FindAll(".fa-shelf-life-row"));
    }

    [Fact]
    public void Member_EditsAndResetsTheProductOverride()
    {
        Catalog.Products.Add(Product());
        var dialogs = Render<MudDialogProvider>();
        var cut = RenderPage(Milk);

        cut.Find("button[aria-label='Bearbeiten: Vollmilch, Gefroren']").Click();
        dialogs.WaitForAssertion(() => Assert.Equal(4, dialogs.FindAll("input").Count));
        dialogs.FindAll("input")[1].Input("30");
        dialogs.FindAll("button").Single(b => b.TextContent.Trim() == "Speichern").Click();
        cut.WaitForAssertion(() => Assert.Contains("30 Tage", cut.FindAll(".fa-shelf-life-row")[1].TextContent, StringComparison.Ordinal));

        var reset = cut.Find("button[aria-label='Auf Kategoriewerte zurücksetzen: Vollmilch, Gekühlt']");
        Assert.Equal("Auf Kategoriewerte zurücksetzen", reset.TextContent.Trim());
        reset.Click();

        Assert.Equal(["product", "sources", "set product/Frozen", "reset product/Chilled"], _client.Calls);
        Assert.DoesNotContain("Eigen", cut.FindAll(".fa-shelf-life-row")[0].TextContent, StringComparison.Ordinal);
    }

    private static ProductDto Product() =>
        new(Milk, "Vollmilch", null, "dairy", "l", null, null, null, [], false, 1);
}
