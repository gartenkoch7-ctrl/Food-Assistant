using Bunit;
using FoodAssistant.UI.Inventory;
using FoodAssistant.UI.Shell;
using FoodAssistant.UI.Shopping;
using FoodAssistant.UI.Tests.Support;

namespace FoodAssistant.UI.Tests.Shell;

/// <summary>The M0 pages are empty states with real text (not placeholders), a single h1 and a useful title.</summary>
public sealed class PagesTests : UiTestContext
{
    [Fact]
    public void Inventory_ExplainsTheEmptyPantry_AndOffersScanning()
    {
        var cut = Render<InventoryPage>();

        Assert.Equal("Vorrat", cut.Find("h1").TextContent);
        Assert.Equal("Dein Vorrat ist noch leer", cut.Find("h2").TextContent);
        Assert.Equal("/scan", cut.Find("a.mud-button-root").GetAttribute("href"));
        Assert.Contains("Artikel scannen", cut.Find("a.mud-button-root").TextContent);
    }

    [Fact]
    public void Shopping_ExplainsTheEmptyList()
    {
        var cut = Render<ShoppingPage>();

        Assert.Equal("Einkauf", cut.Find("h1").TextContent);
        Assert.Equal("Deine Einkaufsliste ist leer", cut.Find("h2").TextContent);
    }

    [Fact]
    public void Scan_SaysItComesInM1_AndKeepsTheManualAlternativeVisible()
    {
        var cut = Render<ScanPage>();

        Assert.Equal("Scannen", cut.Find("h1").TextContent);
        Assert.Contains("kommt in M1", cut.Find("h2").TextContent);
        var manual = cut.Find("button.mud-button-root");
        Assert.Contains("Manuell eingeben", manual.TextContent);
        Assert.True(manual.HasAttribute("disabled"));
        Assert.Equal("scan-manual-hint", manual.GetAttribute("aria-describedby"));
        Assert.Contains("M1", cut.Find("#scan-manual-hint").TextContent);
    }

    [Fact]
    public void NotFound_LeadsBackToThePantry()
    {
        var cut = Render<NotFoundPage>();

        Assert.Equal("Seite nicht gefunden", cut.Find("h1").TextContent);
        Assert.Equal("/", cut.Find("a.mud-button-root").GetAttribute("href"));
    }

    [Fact]
    public void Pages_FollowTheEnglishCulture()
    {
        UseCulture("en");

        Assert.Equal("Pantry", Render<InventoryPage>().Find("h1").TextContent);
        Assert.Equal("Shopping", Render<ShoppingPage>().Find("h1").TextContent);
        Assert.Equal("Scan", Render<ScanPage>().Find("h1").TextContent);
        Assert.Equal("More", Render<MorePage>().Find("h1").TextContent);
    }
}
