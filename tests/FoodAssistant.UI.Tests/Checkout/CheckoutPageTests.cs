using Bunit;
using FoodAssistant.Client.Http;
using FoodAssistant.Contracts.Catalog;
using FoodAssistant.Contracts.Checkout;
using FoodAssistant.Contracts.Common;
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
        Assert.Contains(Row(cut, "Milch").QuerySelectorAll("label"), l => l.TextContent.Contains("Lagerort für Milch", StringComparison.Ordinal));
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
    public async Task FreeText_AssignedProduct_IsSentWithItsId_AndItsDefaults()
    {
        var oat = Product("Haferdrink");
        Catalog.Products.Add(oat);
        Catalog.Categories.Add(new CategoryDto("plant_based", "dairy_chilled", StorageTypeDto.Chilled));
        AddEntry("Hafer", null, Freezer.Id, quantity: new QuantityDto(1m, "piece"));
        var cut = RenderPage();

        var input = cut.Find("li.fa-checkout-row input");
        input.Focus();
        input.Input("Hafer");
        Popovers.WaitForAssertion(() => Assert.NotEmpty(Popovers.FindAll(".mud-list-item")), TimeSpan.FromSeconds(10));
        await Popovers.InvokeAsync(() => Popovers.FindAll(".mud-list-item")[0].Click()).WaitAsync(Timeout, Ct);
        cut.WaitForAssertion(() => Assert.Contains("Eintrag auf der Liste: Hafer", cut.Markup, StringComparison.Ordinal), TimeSpan.FromSeconds(10));
        await cut.InvokeAsync(() => BookButton(cut).Click()).WaitAsync(Timeout, Ct);

        var line = Assert.Single(Assert.Single(CheckoutClient.Completions).Request.Lines);
        Assert.Equal((oat.Id, false, (Guid?)Fridge.Id, new QuantityDto(1m, "l")), (line.ProductId, line.Skip, line.StorageLocationId, line.Quantity));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CrossingTheFreezerBoundary_DropsDatesThePersonDidNotEnter(bool ruleDate)
    {
        var (from, to) = ruleDate ? (Freezer, Fridge) : (Fridge, Freezer);
        AddEntry("Erbsen", Guid.CreateVersion7(), from.Id, Today.AddDays(90), ruleDate ? ExpiryKindDto.UseBy : ExpiryKindDto.BestBefore, fromRule: ruleDate);
        var cut = RenderPage();
        var line = cut.FindComponent<FoodAssistant.UI.Checkout.CheckoutLineRow>().Instance.Line;

        await cut.InvokeAsync(() => line.MoveTo(to.Id, freezer: !ruleDate, fromFreezer: ruleDate)).WaitAsync(Timeout, Ct);
        cut.Render();

        var request = line.ToRequest();
        Assert.Equal((to.Id, (DateOnly?)null, ExpiryKindDto.None), (request.StorageLocationId!.Value, request.ExpiresOn, request.ExpiryKind));
        Assert.DoesNotContain("16.02.2027", Row(cut, "Erbsen").TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void MovingWithinTheSameKindOfPlace_KeepsTheSuggestedDate()
    {
        var pantry = new StorageLocationDto(Guid.CreateVersion7(), "Vorratsschrank", StorageTypeDto.Ambient, 2, 1);
        var line = new FoodAssistant.UI.Checkout.CheckoutLine(AddEntry("Reis", Guid.CreateVersion7(), Fridge.Id, Today.AddDays(30), ExpiryKindDto.BestBefore));

        line.MoveTo(pantry.Id, freezer: false, fromFreezer: false);

        Assert.Equal((Today.AddDays(30), ExpiryKindDto.BestBefore), (line.ToRequest().ExpiresOn!.Value, line.ToRequest().ExpiryKind));
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
        Assert.Contains("1 Eintrag wurde nicht gebucht", cut.Markup, StringComparison.Ordinal);
        await cut.InvokeAsync(() => BookButton(cut).Click()).WaitAsync(Timeout, Ct);

        Assert.Equal(3, CheckoutClient.Completions.Count);
        Assert.Equal(CheckoutClient.Completions[0].Key, CheckoutClient.Completions[1].Key);
        Assert.NotEqual(CheckoutClient.Completions[1].Key, CheckoutClient.Completions[2].Key);
        Assert.Single(CheckoutClient.Completions[2].Request.Lines);

        // The hint belonged to the previous attempt; the next one starts without it.
        Assert.DoesNotContain("1 Eintrag wurde nicht gebucht", cut.Markup, StringComparison.Ordinal);
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
    public async Task RequestStillInProgress_KeepsTheKey_AllSkipped_SaysNothingWasAdded()
    {
        var bread = AddEntry("Brot", Guid.CreateVersion7(), Fridge.Id);
        CheckoutClient.Answers.Enqueue(ApiResult<CheckoutResultDto>.Failure(
            new ApiError(409, ErrorCodes.IdempotencyRequestInProgress, null, new Dictionary<string, string[]>(), null)));
        var cut = RenderPage();
        await cut.InvokeAsync(() => RowButton(cut, "Brot", "Überspringen").Click()).WaitAsync(Timeout, Ct);

        await cut.InvokeAsync(() => BookButton(cut).Click()).WaitAsync(Timeout, Ct);
        await cut.InvokeAsync(() => BookButton(cut).Click()).WaitAsync(Timeout, Ct);

        Assert.Equal(CheckoutClient.Completions[0].Key, CheckoutClient.Completions[1].Key);
        Assert.True(Assert.Single(CheckoutClient.Completions[1].Request.Lines, l => l.ItemId == bread.ItemId).Skip);
        Assert.Equal("Nichts gebucht. Von der Liste entfernt: 1.", Services.GetRequiredService<ILiveAnnouncer>().Message);
    }

    [Fact]
    public async Task Conflict_ReloadsTheSuggestions_AndSaysWhy()
    {
        AddEntry("Milch", Guid.CreateVersion7(), Fridge.Id);
        CheckoutClient.Answers.Enqueue(ApiResult<CheckoutResultDto>.Failure(
            new ApiError(409, ErrorCodes.Conflict, null, new Dictionary<string, string[]>(), null)));
        var cut = RenderPage();

        await cut.InvokeAsync(() => BookButton(cut).Click()).WaitAsync(Timeout, Ct);

        Assert.Equal(2, CheckoutClient.ProposalCalls);
        Assert.Contains("Lagerorte haben sich inzwischen geändert", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LocationRetypedSinceLoading_DropsTheSuggestion_AndAsksToCheckFirst()
    {
        AddEntry("Spinat", Guid.CreateVersion7(), Fridge.Id, Today.AddDays(7), ExpiryKindDto.BestBefore);
        var cut = RenderPage();
        Catalog.Locations[Catalog.Locations.IndexOf(Fridge)] = Fridge with { Type = StorageTypeDto.Frozen };

        await cut.InvokeAsync(() => BookButton(cut).Click()).WaitAsync(Timeout, Ct);

        Assert.Empty(CheckoutClient.Completions);
        Assert.Contains("Lagerorte haben sich inzwischen geändert", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("25.11.2026", Row(cut, "Spinat").TextContent, StringComparison.Ordinal);

        await cut.InvokeAsync(() => BookButton(cut).Click()).WaitAsync(Timeout, Ct);
        var line = Assert.Single(Assert.Single(CheckoutClient.Completions).Request.Lines);
        Assert.Equal(((DateOnly?)null, ExpiryKindDto.None), (line.ExpiresOn, line.ExpiryKind));
    }

    [Fact]
    public async Task ProductArchivedMeanwhile_OffersThePickerAgain()
    {
        AddEntry("Butter", Guid.CreateVersion7(), Fridge.Id);
        CheckoutClient.Answers.Enqueue(ApiResult<CheckoutResultDto>.Failure(new ApiError(400, CheckoutErrorCodes.ValidationFailed, null,
            new Dictionary<string, string[]> { ["lines[0].productId"] = ["not_found"] }, null)));
        var cut = RenderPage();

        await cut.InvokeAsync(() => BookButton(cut).Click()).WaitAsync(Timeout, Ct);

        Assert.Contains(Row(cut, "Butter").QuerySelectorAll("label"), l => l.TextContent.Contains("Produkt für „Butter“", StringComparison.Ordinal));
        Assert.Contains("Produkt: Gibt es nicht mehr.", Row(cut, "Butter").TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void NothingChecked_ShowsTheEmptyState()
    {
        var cut = RenderPage();

        Assert.Contains("Nichts im Wagen", cut.Markup, StringComparison.Ordinal);
        Assert.Empty(cut.FindAll("li.fa-checkout-row"));
    }
}
