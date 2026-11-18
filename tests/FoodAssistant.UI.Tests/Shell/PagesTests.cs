using Bunit;
using FoodAssistant.UI.Inventory;
using FoodAssistant.UI.Scan;
using FoodAssistant.UI.Shell;
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
    public void Scan_HasASingleH1_AndKeepsTheManualAlternativeVisible()
    {
        var cut = Render<ScanPage>();

        Assert.Equal("Scannen", cut.Find("h1").TextContent);
        Assert.Single(cut.FindAll("h1"));
        Assert.NotNull(cut.Find("#scan-barcode-input"));
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
        Assert.Equal("Scan", Render<ScanPage>().Find("h1").TextContent);
        Assert.Equal("More", Render<MorePage>().Find("h1").TextContent);
    }
}
