using Bunit;
using FoodAssistant.UI.Common;
using FoodAssistant.UI.Scan;
using FoodAssistant.UI.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace FoodAssistant.UI.Tests.Scan;

/// <summary>The scan page: camera is optional, manual entry is always there, results are announced.</summary>
public sealed class ScanPageTests : ScanFlowTestContext
{
    private string Announced => Services.GetRequiredService<ILiveAnnouncer>().Message.Trim();

    private IRenderedComponent<ScanPage> RenderPage()
    {
        var cut = Render<ScanPage>();
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("h2")));
        return cut;
    }

    private void StockEggs(decimal amount)
    {
        var eggs = KnownProduct("piece");
        StockOf(eggs, PieceBatch(eggs, amount));
    }

    private static void EnterBarcode(IRenderedComponent<ScanPage> cut, string digits)
    {
        cut.Find("#scan-barcode-input").Input(digits);
        cut.Find("form").Submit();
    }

    private static void ChooseUse(IRenderedComponent<ScanPage> cut) => cut.FindAll("input[type=radio]")[1].Change(true);

    [Fact]
    public void WithoutACamera_ManualEntryAndSearchAreThere_AndTheHintSaysSo()
    {
        var cut = RenderPage();

        cut.WaitForAssertion(() => Assert.Contains("Die Kamera ist hier nicht verfügbar", cut.Markup, StringComparison.Ordinal));
        Assert.Equal("Scannen", cut.Find("h1").TextContent);
        Assert.NotNull(cut.Find("#scan-barcode-input"));
        Assert.Contains("Produkt suchen", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("Kamera starten", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void ModeSwitch_OffersAddAndUsed_AddIsPreselected()
    {
        var cut = RenderPage();

        var labels = cut.FindAll("label.fa-option").Select(l => l.TextContent.Trim()).ToList();
        Assert.Equal(["Einbuchen", "Verbraucht"], labels);
        Assert.True(cut.FindAll("input[type=radio]")[0].HasAttribute("checked"));
    }

    [Fact]
    public void CameraButton_StartsAndStopsTheScanner()
    {
        Scanner.Available = true;
        var cut = RenderPage();

        cut.WaitForAssertion(() => Assert.Contains("Kamera starten", cut.Markup, StringComparison.Ordinal));
        Assert.Contains("Das Kamerabild bleibt auf deinem Gerät", cut.Markup, StringComparison.Ordinal);
        cut.FindAll("button").First(b => b.TextContent.Contains("Kamera starten", StringComparison.Ordinal)).Click();

        cut.WaitForAssertion(() => Assert.Contains("Kamera stoppen", cut.Markup, StringComparison.Ordinal));
        Assert.Equal(("scan-video", true), (Scanner.VideoElementId, Scanner.Running));
        Assert.False(cut.Find("video").HasAttribute("hidden"));
        cut.FindAll("button").First(b => b.TextContent.Contains("Kamera stoppen", StringComparison.Ordinal)).Click();

        cut.WaitForAssertion(() => Assert.True(cut.Find("video").HasAttribute("hidden")));
        Assert.False(Scanner.Running);
    }

    [Fact]
    public void CameraDenied_ShowsAHint_AndKeepsManualEntry()
    {
        Scanner.Available = true;
        Scanner.CanStart = false;
        var cut = RenderPage();

        cut.WaitForAssertion(() => Assert.Contains("Kamera starten", cut.Markup, StringComparison.Ordinal));
        cut.FindAll("button").First(b => b.TextContent.Contains("Kamera starten", StringComparison.Ordinal)).Click();

        cut.WaitForAssertion(() => Assert.Contains("Die Kamera ließ sich nicht starten", cut.Find("[role=alert]").TextContent, StringComparison.Ordinal));
        Assert.NotNull(cut.Find("#scan-barcode-input"));
    }

    [Fact]
    public async Task LeavingThePage_StopsTheCamera()
    {
        Scanner.Available = true;
        var cut = RenderPage();
        cut.WaitForAssertion(() => Assert.Contains("Kamera starten", cut.Markup, StringComparison.Ordinal));
        cut.FindAll("button").First(b => b.TextContent.Contains("Kamera starten", StringComparison.Ordinal)).Click();
        cut.WaitForAssertion(() => Assert.True(Scanner.Running));

        await DisposeComponentsAsync();

        Assert.False(Scanner.Running);
    }

    [Fact]
    public void ManualBarcode_InUsedMode_UsesOnePiece_ShowsAndAnnouncesTheResult_AndFocusesTheField()
    {
        StockEggs(6m);
        var cut = RenderPage();
        ChooseUse(cut);

        EnterBarcode(cut, Barcode);

        cut.WaitForAssertion(() => Assert.Contains("Milch: verbraucht", cut.Find(".fa-scan-result").TextContent, StringComparison.Ordinal));
        Assert.Equal("Milch: verbraucht", Announced);
        Assert.Contains("Rückgängig", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("Menge ändern", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("scan-barcode-input", Browser.FocusedElements);
        Assert.Equal(string.Empty, cut.Find("#scan-barcode-input").GetAttribute("value") ?? string.Empty);
    }

    [Fact]
    public void Undo_RevertsAndSaysSo()
    {
        StockEggs(6m);
        var cut = RenderPage();
        ChooseUse(cut);
        EnterBarcode(cut, Barcode);
        cut.WaitForAssertion(() => Assert.Contains("Rückgängig", cut.Markup, StringComparison.Ordinal));

        cut.FindAll("button").First(b => b.TextContent.Trim() == "Rückgängig").Click();

        cut.WaitForAssertion(() => Assert.Equal("Rückgängig gemacht.", cut.Find(".fa-scan-result").TextContent.Trim()));
        Assert.Contains(Inventory.Calls, c => c.StartsWith("undo:", StringComparison.Ordinal));
        Assert.DoesNotContain("Menge ändern", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void AddedBatch_OffersUndoButNotChangeAmount()
    {
        KnownProduct();
        var cut = RenderPage();

        EnterBarcode(cut, Barcode);
        WaitForDialogText("Lagerort");
        Click("Speichern");

        cut.WaitForAssertion(() => Assert.Equal("Milch: eingebucht", cut.Find(".fa-scan-result").TextContent.Trim()));
        Assert.Contains("Rückgängig", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("Menge ändern", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void NotABarcode_SaysSo_WithoutCallingTheServer()
    {
        var cut = RenderPage();

        EnterBarcode(cut, "12ab");

        cut.WaitForAssertion(() => Assert.Contains("weder ein Barcode noch ein Etikett", cut.Find(".fa-scan-result").TextContent, StringComparison.Ordinal));
        Assert.DoesNotContain(Catalog.Calls, c => c.StartsWith("barcode:", StringComparison.Ordinal));
    }

    [Fact]
    public void ServerError_ShowsTheErrorAlert()
    {
        Catalog.FailWith = FakeAccountClient.Error(500, "common.internal_error");
        var cut = RenderPage();

        EnterBarcode(cut, Barcode);

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".fa-api-error")));
    }

    [Fact]
    public async Task CodeFromTheCamera_RunsTheSameFlow()
    {
        StockEggs(6m);
        Scanner.Available = true;
        var cut = RenderPage();
        ChooseUse(cut);
        cut.WaitForAssertion(() => Assert.Contains("Kamera starten", cut.Markup, StringComparison.Ordinal));
        cut.FindAll("button").First(b => b.TextContent.Contains("Kamera starten", StringComparison.Ordinal)).Click();
        cut.WaitForAssertion(() => Assert.True(Scanner.Running));

        await cut.InvokeAsync(() => Scanner.EmitAsync(Barcode));

        cut.WaitForAssertion(() => Assert.Contains("Milch: verbraucht", cut.Markup, StringComparison.Ordinal));
        // The camera keeps focus: no keyboard pops up after a camera scan.
        Assert.DoesNotContain("scan-barcode-input", Browser.FocusedElements);
    }

    [Fact]
    public void EnglishCulture_UsesEnglishTexts()
    {
        UseCulture("en");
        var cut = RenderPage();

        Assert.Equal("Scan", cut.Find("h1").TextContent);
        Assert.Equal(["Add to stock", "Used"], cut.FindAll("label.fa-option").Select(l => l.TextContent.Trim()));
        Assert.Contains("Enter barcode", cut.Markup, StringComparison.Ordinal);
    }
}
