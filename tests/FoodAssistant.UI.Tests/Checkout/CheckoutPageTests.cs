using Bunit;
using FoodAssistant.Client.Http;
using FoodAssistant.Contracts.Checkout;
using FoodAssistant.Contracts.Inventory;
using FoodAssistant.UI.Common;
using Microsoft.Extensions.DependencyInjection;

namespace FoodAssistant.UI.Tests.Checkout;

public sealed class CheckoutPageTests : CheckoutTestContext
{
    [Fact]
    public void Rows_ShowNameAmountLocationAndDate_RuleDateAsText()
    {
        AddEntry("Milch", Guid.CreateVersion7(), Fridge.Id, Today.AddDays(7), ExpiryKindDto.BestBefore);
        AddEntry("Erbsen", Guid.CreateVersion7(), Freezer.Id, Today.AddDays(90), ExpiryKindDto.UseBy, fromRule: true);

        var cut = RenderPage();

        Assert.Equal(["Milch", "Erbsen"], cut.FindAll("li.fa-checkout-row h2").Select(h => h.TextContent.Trim()));
        Assert.Contains("1 l · MHD: 25.11.2026", Row(cut, "Milch").TextContent, StringComparison.Ordinal);
        Assert.Contains("Verbrauchsdatum: 16.02.2027 · aus Regel", Row(cut, "Erbsen").TextContent, StringComparison.Ordinal);
        Assert.Contains("In den Vorrat buchen (2)", BookButton(cut).TextContent, StringComparison.Ordinal);
        Assert.Equal("Milch ändern", RowButton(cut, "Milch", "Ändern").GetAttribute("aria-label"));
    }

    [Fact]
    public async Task FreeText_MustBeAssignedOrSkipped_SkipSendsIt_RuleDateIsNotSent()
    {
        var peas = AddEntry("Erbsen", Guid.CreateVersion7(), Freezer.Id, Today.AddDays(90), ExpiryKindDto.UseBy, fromRule: true);
        var bread = AddEntry("Brot", null, Fridge.Id);
        var cut = RenderPage();

        await cut.InvokeAsync(() => BookButton(cut).Click()).WaitAsync(Timeout, Ct);

        Assert.Empty(CheckoutClient.Completions);
        Assert.Contains("Bitte ein Produkt zuordnen oder überspringen.", Row(cut, "Brot").TextContent, StringComparison.Ordinal);
        Assert.Contains("Bitte prüfe die markierten Zeilen.", cut.Markup, StringComparison.Ordinal);

        await cut.InvokeAsync(() => RowButton(cut, "Brot", "Überspringen").Click()).WaitAsync(Timeout, Ct);
        Assert.Contains("In den Vorrat buchen (1)", BookButton(cut).TextContent, StringComparison.Ordinal);
        await cut.InvokeAsync(() => BookButton(cut).Click()).WaitAsync(Timeout, Ct);

        var (request, _) = Assert.Single(CheckoutClient.Completions);
        Assert.Equal(new CheckoutLineRequest(peas.ItemId, 2, false, peas.ProductId, null, peas.Quantity, Freezer.Id, null, ExpiryKindDto.None, false), request.Lines[0]);
        Assert.Equal((bread.ItemId, true), (request.Lines[1].ItemId, request.Lines[1].Skip));
        cut.WaitForAssertion(() => Assert.EndsWith($"/shopping/{ListId:D}", CurrentUri, StringComparison.Ordinal));
        Assert.Equal("1 Charge in den Vorrat gebucht.", Services.GetRequiredService<ILiveAnnouncer>().Message);
    }

    [Fact]
    public async Task FreeText_AssignedProduct_IsSentWithItsId()
    {
        var oat = Product("Haferdrink");
        Catalog.Products.Add(oat);
        AddEntry("Hafer", null, Fridge.Id);
        var cut = RenderPage();

        var input = cut.Find("li.fa-checkout-row input");
        input.Focus();
        input.Input("Hafer");
        Popovers.WaitForAssertion(() => Assert.NotEmpty(Popovers.FindAll(".mud-list-item")), TimeSpan.FromSeconds(10));
        await Popovers.InvokeAsync(() => Popovers.FindAll(".mud-list-item")[0].Click()).WaitAsync(Timeout, Ct);
        cut.WaitForAssertion(() => Assert.Contains("Eintrag auf der Liste: Hafer", cut.Markup, StringComparison.Ordinal), TimeSpan.FromSeconds(10));
        await cut.InvokeAsync(() => BookButton(cut).Click()).WaitAsync(Timeout, Ct);

        var line = Assert.Single(Assert.Single(CheckoutClient.Completions).Request.Lines);
        Assert.Equal((oat.Id, false), (line.ProductId, line.Skip));
    }

    [Fact]
    public async Task LeavingTheFreezer_DropsTheRuleDate()
    {
        var peas = AddEntry("Erbsen", Guid.CreateVersion7(), Freezer.Id, Today.AddDays(90), ExpiryKindDto.UseBy, fromRule: true);
        var cut = RenderPage();
        var line = cut.FindComponent<FoodAssistant.UI.Checkout.CheckoutLineRow>().Instance.Line;

        await cut.InvokeAsync(() => line.MoveTo(Fridge.Id, freezer: false)).WaitAsync(Timeout, Ct);
        cut.Render();

        Assert.Contains("kein Datum", Row(cut, "Erbsen").TextContent, StringComparison.Ordinal);
        Assert.Equal((Fridge.Id, (DateOnly?)null, ExpiryKindDto.None), (line.ToRequest().StorageLocationId, line.ToRequest().ExpiresOn, line.ToRequest().ExpiryKind));
        Assert.Equal(peas.ItemId, line.ItemId);
    }

    [Fact]
    public async Task NetworkFailure_RetryKeepsTheKey_StaleEntriesAreReported()
    {
        var milk = AddEntry("Milch", Guid.CreateVersion7(), Fridge.Id);
        AddEntry("Käse", Guid.CreateVersion7(), Fridge.Id);
        CheckoutClient.Answers.Enqueue(ApiResult<CheckoutResultDto>.Failure(new ApiError(0, ClientErrorCodes.Network, null, new Dictionary<string, string[]>(), null)));
        CheckoutClient.Answers.Enqueue(ApiResult<CheckoutResultDto>.Success(new CheckoutResultDto(1, 1, [Guid.CreateVersion7()], [milk.ItemId])));
        var cut = RenderPage();

        await cut.InvokeAsync(() => BookButton(cut).Click()).WaitAsync(Timeout, Ct);
        Assert.Contains("Der Server ist nicht erreichbar", cut.Markup, StringComparison.Ordinal);
        CheckoutClient.Entries.RemoveAt(1);
        await cut.InvokeAsync(() => BookButton(cut).Click()).WaitAsync(Timeout, Ct);
        await cut.InvokeAsync(() => BookButton(cut).Click()).WaitAsync(Timeout, Ct);

        Assert.Equal(3, CheckoutClient.Completions.Count);
        Assert.Equal(CheckoutClient.Completions[0].Key, CheckoutClient.Completions[1].Key);
        Assert.NotEqual(CheckoutClient.Completions[1].Key, CheckoutClient.Completions[2].Key);
        Assert.Single(CheckoutClient.Completions[2].Request.Lines);
        Assert.Contains("1 Einträge wurden inzwischen geändert", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ServerFieldError_IsShownAtItsLine()
    {
        AddEntry("Milch", Guid.CreateVersion7(), Fridge.Id);
        CheckoutClient.Answers.Enqueue(ApiResult<CheckoutResultDto>.Failure(new ApiError(400, CheckoutErrorCodes.ValidationFailed, null,
            new Dictionary<string, string[]> { ["lines[0].storageLocationId"] = ["not_found"] }, null)));
        var cut = RenderPage();

        await cut.InvokeAsync(() => BookButton(cut).Click()).WaitAsync(Timeout, Ct);

        Assert.Contains("Lagerort: Gibt es nicht mehr.", Row(cut, "Milch").TextContent, StringComparison.Ordinal);
        Assert.Contains("Bitte prüfe die markierten Zeilen.", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void NothingChecked_ShowsTheEmptyState()
    {
        var cut = RenderPage();

        Assert.Contains("Nichts im Wagen", cut.Markup, StringComparison.Ordinal);
        Assert.Empty(cut.FindAll("li.fa-checkout-row"));
    }
}
