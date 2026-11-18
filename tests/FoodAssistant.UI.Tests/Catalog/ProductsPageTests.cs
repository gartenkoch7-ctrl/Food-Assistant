using Bunit;
using FoodAssistant.Client.Http;
using FoodAssistant.Contracts.Households;
using FoodAssistant.Contracts.Sync;
using FoodAssistant.UI.Catalog;

namespace FoodAssistant.UI.Tests.Catalog;

public sealed class ProductsPageTests : CatalogTestContext
{
    [Fact]
    public void NoProducts_ShowsTheEmptyStateWithTheCreateButton()
    {
        var cut = Render<ProductsPage>();

        Assert.Equal("Produkte", cut.Find("h1").TextContent);
        Assert.Contains("Noch keine Produkte", cut.Find(".fa-empty-heading").TextContent, StringComparison.Ordinal);
        Assert.Contains(cut.FindAll("button"), b => b.TextContent.Contains("Produkt anlegen", StringComparison.Ordinal));
    }

    [Fact]
    public void Products_AreListedWithBrandAndCategory()
    {
        Catalog.Products.Add(Product("Vollmilch", "dairy", "Hof"));
        Catalog.Products.Add(Product("Brötchen", "bread"));

        var cut = Render<ProductsPage>();

        var rows = cut.FindAll("li.fa-list-row");
        Assert.Equal(2, rows.Count);
        Assert.Contains("Vollmilch", rows[0].TextContent, StringComparison.Ordinal);
        Assert.Contains("Hof · ", rows[0].TextContent, StringComparison.Ordinal);
        Assert.DoesNotContain("·", rows[1].TextContent, StringComparison.Ordinal);
        Assert.Equal("Produkte", cut.Find("ul.fa-list").GetAttribute("aria-label"));
    }

    [Fact]
    public void EachRow_HasAnEditButtonNamedAfterTheProduct()
    {
        Catalog.Products.Add(Product("Vollmilch"));

        var cut = Render<ProductsPage>();

        Assert.Equal("Vollmilch bearbeiten", cut.Find("li.fa-list-row button").GetAttribute("aria-label"));
    }

    [Fact]
    public void Search_AsksTheServer_AndShowsNoResultsText()
    {
        Catalog.Products.Add(Product("Vollmilch"));
        Catalog.Products.Add(Product("Brötchen", "bread"));
        var cut = Render<ProductsPage>();

        cut.Find("input").Input("milch");
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("li.fa-list-row")));
        cut.Find("input").Input("xyz");
        cut.WaitForAssertion(() => Assert.Contains("Keine Treffer", cut.Find(".fa-empty-heading").TextContent, StringComparison.Ordinal));

        Assert.Contains("search:milch", Catalog.Calls);
    }

    [Fact]
    public void LoadMore_AppendsTheNextPage_AndDisappearsOnTheLast()
    {
        Catalog.PageSize = 2;
        foreach (var name in new[] { "A", "B", "C" })
        {
            Catalog.Products.Add(Product(name));
        }

        var cut = Render<ProductsPage>();
        Assert.Equal(2, cut.FindAll("li.fa-list-row").Count);

        cut.Find("button.mud-button-outlined").Click();

        Assert.Equal(3, cut.FindAll("li.fa-list-row").Count);
        Assert.Empty(cut.FindAll("button.mud-button-outlined"));
    }

    [Fact]
    public async Task LiveChangeOfAProduct_ReloadsTheList()
    {
        var cut = Render<ProductsPage>();
        Catalog.Products.Add(Product("Neu"));

        await Live.RaiseChangedAsync(new HouseholdChangesMessage(
            Session.ActiveHouseholdId!.Value, [new HouseholdChangeDto(SyncEntityTypes.Product, Guid.CreateVersion7(), 1, false)]));

        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("li.fa-list-row")));
    }

    [Fact]
    public async Task LiveChangeOfAnotherKind_DoesNotReload()
    {
        var cut = Render<ProductsPage>();
        var before = Catalog.Calls.Count;

        await Live.RaiseChangedAsync(new HouseholdChangesMessage(
            Session.ActiveHouseholdId!.Value, [new HouseholdChangeDto(SyncEntityTypes.Store, Guid.CreateVersion7(), 1, false)]));

        Assert.Equal(before, Catalog.Calls.Count);
        Assert.NotNull(cut);
    }

    [Fact]
    public void FailedLoad_ShowsTheError()
    {
        Catalog.FailWith = new ApiError(0, "client.network", null, new Dictionary<string, string[]>(), null);

        var cut = Render<ProductsPage>();

        Assert.Contains("Server ist nicht erreichbar", cut.Find("[role=alert]").TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void EditButton_OpensTheDialog_AndSavingReloadsTheList()
    {
        var milk = Product("Vollmilch");
        Catalog.Products.Add(milk);
        var cut = Render<ProductsPage>();

        cut.Find("li.fa-list-row button").Click();
        Dialogs.WaitForAssertion(() => Assert.Contains("Produkt bearbeiten", Dialogs.Markup, StringComparison.Ordinal));
        InputFor(Dialogs, "Name").Change("Bio-Vollmilch");
        ButtonByText(Dialogs, "Speichern").Click();

        cut.WaitForAssertion(() => Assert.Contains("Bio-Vollmilch", cut.Find("li.fa-list-row").TextContent, StringComparison.Ordinal));
        Assert.Contains($"update-product:{milk.Id}", Catalog.Calls);
    }

    [Fact]
    public void CreateButton_OpensTheEmptyDialog()
    {
        var cut = Render<ProductsPage>();

        cut.FindAll("button").First(b => b.TextContent.Contains("Produkt anlegen", StringComparison.Ordinal)).Click();

        Dialogs.WaitForAssertion(() => Assert.Contains("Standardeinheit", Dialogs.Markup, StringComparison.Ordinal));
        Assert.DoesNotContain("Archivieren", Dialogs.Markup, StringComparison.Ordinal);
    }
}

public sealed class ProductsPageRestrictedTests() : CatalogTestContext(role: HouseholdRole.Restricted)
{
    [Fact]
    public void RestrictedMember_SeesTheProducts_ButNoCreateButton()
    {
        Catalog.Products.Add(Product("Vollmilch"));

        var cut = Render<ProductsPage>();

        Assert.Single(cut.FindAll("li.fa-list-row"));
        Assert.DoesNotContain(cut.FindAll("button"), b => b.TextContent.Contains("Produkt anlegen", StringComparison.Ordinal));
    }
}
