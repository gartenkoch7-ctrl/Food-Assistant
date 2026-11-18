using Bunit;
using FoodAssistant.Contracts.Catalog;
using FoodAssistant.UI.Catalog;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace FoodAssistant.UI.Tests.Catalog;

public sealed class ProductPickerTests : CatalogTestContext
{
    private IRenderedComponent<ProductPicker> RenderPicker(Action<ProductSummaryDto> selected, bool allowCreate = true) =>
        Render<ProductPicker>(p => p
            .Add(c => c.AllowCreate, allowCreate)
            .Add(c => c.ProductSelected, EventCallback.Factory.Create<ProductSummaryDto>(this, selected)));

    private static void Type(IRenderedComponent<ProductPicker> cut, string text)
    {
        var input = cut.Find("input");
        input.Focus();
        input.Input(text);
    }

    [Fact]
    public void TheFieldHasAnAccessibleName()
    {
        var cut = RenderPicker(_ => { });

        Assert.Contains(cut.FindAll("label"), l => l.TextContent.Contains("Produkt", StringComparison.Ordinal));
    }

    [Fact]
    public void Typing_ShowsMatchingProducts_AndTheCreateOptionLast()
    {
        Catalog.Products.Add(Product("Vollmilch"));
        Catalog.Products.Add(Product("Brötchen", "bread"));
        var cut = RenderPicker(_ => { });

        Type(cut, "milch");

        Popovers.WaitForAssertion(() =>
        {
            var items = Popovers.FindAll(".mud-list-item");
            Assert.Equal(2, items.Count);
            Assert.Contains("Vollmilch", items[0].TextContent, StringComparison.Ordinal);
            Assert.Contains("„milch“ als neues Produkt anlegen", items[1].TextContent, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void WithoutAllowCreate_ThereIsNoCreateOption()
    {
        Catalog.Products.Add(Product("Vollmilch"));
        var cut = RenderPicker(_ => { }, allowCreate: false);

        Type(cut, "milch");

        Popovers.WaitForAssertion(() => Assert.Single(Popovers.FindAll(".mud-list-item")));
    }

    [Fact]
    public void ChoosingAProduct_RaisesProductSelected()
    {
        var milk = Product("Vollmilch");
        Catalog.Products.Add(milk);
        ProductSummaryDto? picked = null;
        var cut = RenderPicker(p => picked = p);

        Type(cut, "milch");
        Popovers.WaitForAssertion(() => Assert.NotEmpty(Popovers.FindAll(".mud-list-item")));
        Popovers.FindAll(".mud-list-item")[0].Click();

        cut.WaitForAssertion(() => Assert.Equal(milk.Id, picked?.Id));
    }

    [Fact]
    public void ChoosingCreate_OpensTheProductDialogWithTheTypedName_AndSelectsTheNewProduct()
    {
        ProductSummaryDto? picked = null;
        var cut = RenderPicker(p => picked = p);

        Type(cut, "Haferdrink");
        Popovers.WaitForAssertion(() => Assert.Single(Popovers.FindAll(".mud-list-item")));
        Popovers.Find(".mud-list-item").Click();
        Dialogs.WaitForAssertion(() => Assert.Equal("Haferdrink", InputFor(Dialogs, "Name").GetAttribute("value")));
        ButtonByText(Dialogs, "Speichern").Click();

        cut.WaitForAssertion(() => Assert.Equal("Haferdrink", picked?.Name));
        Assert.Single(Catalog.Products);
    }
}
