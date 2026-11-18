using Bunit;
using FoodAssistant.Client.Http;
using FoodAssistant.Contracts.Checkout;
using FoodAssistant.Contracts.Inventory;

namespace FoodAssistant.UI.Tests.Checkout;

/// <summary>Things that change while the page is open: locations retyped or deleted, products archived.</summary>
public sealed class CheckoutPageChangesTests : CheckoutTestContext
{
    [Fact]
    public async Task LocationRetypedSinceLoading_DropsTheSuggestion_AndAsksToCheckFirst()
    {
        AddEntry("Spinat", Guid.CreateVersion7(), Fridge.Id, Today.AddDays(7), ExpiryKindDto.BestBefore);
        var cut = RenderPage();
        Catalog.Locations[Catalog.Locations.IndexOf(Fridge)] = Fridge with { Type = StorageTypeDto.Frozen };

        await cut.InvokeAsync(() => BookButton(cut).Click()).WaitAsync(Timeout, Ct);

        Assert.Empty(CheckoutClient.Completions);
        Assert.Contains("Ein Lagerort wurde inzwischen geändert oder gelöscht", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("25.11.2026", Row(cut, "Spinat").TextContent, StringComparison.Ordinal);

        await cut.InvokeAsync(() => BookButton(cut).Click()).WaitAsync(Timeout, Ct);
        var line = Assert.Single(Assert.Single(CheckoutClient.Completions).Request.Lines);
        Assert.Equal(((DateOnly?)null, ExpiryKindDto.None), (line.ExpiresOn, line.ExpiryKind));
    }

    [Fact]
    public async Task LocationRetyped_SkippedLineBookedAfterAll_HasNoDateForTheOldType()
    {
        var spinach = AddEntry("Spinat", Guid.CreateVersion7(), Fridge.Id, Today.AddDays(7), ExpiryKindDto.BestBefore);
        AddEntry("Milch", Guid.CreateVersion7(), Fridge.Id);
        var cut = RenderPage();
        await cut.InvokeAsync(() => RowButton(cut, "Spinat", "Überspringen").Click()).WaitAsync(Timeout, Ct);
        Catalog.Locations[Catalog.Locations.IndexOf(Fridge)] = Fridge with { Type = StorageTypeDto.Frozen };

        await cut.InvokeAsync(() => BookButton(cut).Click()).WaitAsync(Timeout, Ct);
        await cut.InvokeAsync(() => RowButton(cut, "Spinat", "Doch buchen").Click()).WaitAsync(Timeout, Ct);
        await cut.InvokeAsync(() => BookButton(cut).Click()).WaitAsync(Timeout, Ct);

        var line = Assert.Single(Assert.Single(CheckoutClient.Completions).Request.Lines, l => l.ItemId == spinach.ItemId);
        Assert.Equal((false, (DateOnly?)null, ExpiryKindDto.None), (line.Skip, line.ExpiresOn, line.ExpiryKind));
    }

    [Fact]
    public async Task LocationDeletedSinceLoading_ClearsItAndTheFreezerDate_ThenAsksForALocation()
    {
        var peas = AddEntry("Erbsen", Guid.CreateVersion7(), Freezer.Id, Today.AddDays(90), ExpiryKindDto.UseBy, fromRule: true);
        var cut = RenderPage();
        Catalog.Locations.Remove(Freezer);

        await cut.InvokeAsync(() => BookButton(cut).Click()).WaitAsync(Timeout, Ct);
        Assert.Contains("Ein Lagerort wurde inzwischen geändert oder gelöscht", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("16.02.2027", Row(cut, "Erbsen").TextContent, StringComparison.Ordinal);
        await cut.InvokeAsync(() => BookButton(cut).Click()).WaitAsync(Timeout, Ct);

        Assert.Empty(CheckoutClient.Completions);
        Assert.Contains("Lagerort: Bitte ausfüllen.", Row(cut, "Erbsen").TextContent, StringComparison.Ordinal);
        Assert.Equal(peas.ItemId, cut.FindComponent<FoodAssistant.UI.Checkout.CheckoutLineRow>().Instance.Line.ItemId);
    }

    [Fact]
    public async Task ProductArchivedMeanwhile_OffersThePickerAgain_AndAnotherProductLater()
    {
        var butter = Product("Butter, irisch");
        Catalog.Products.Add(butter);
        AddEntry("Butter", Guid.CreateVersion7(), Fridge.Id);
        CheckoutClient.Answers.Enqueue(ApiResult<CheckoutResultDto>.Failure(new ApiError(400, CheckoutErrorCodes.ValidationFailed, null,
            new Dictionary<string, string[]> { ["lines[0].productId"] = ["not_found"] }, null)));
        var cut = RenderPage();

        await cut.InvokeAsync(() => BookButton(cut).Click()).WaitAsync(Timeout, Ct);
        Assert.Contains(Row(cut, "Butter").QuerySelectorAll("label"), l => l.TextContent.Contains("Produkt für „Butter“", StringComparison.Ordinal));
        Assert.Contains("Produkt: Gibt es nicht mehr.", Row(cut, "Butter").TextContent, StringComparison.Ordinal);
        Assert.Empty(Row(cut, "Butter").QuerySelectorAll("[aria-invalid='true']"));

        var input = cut.Find("li.fa-checkout-row input");
        input.Focus();
        input.Input("Butter");
        Popovers.WaitForAssertion(() => Assert.NotEmpty(Popovers.FindAll(".mud-list-item")), Timeout);
        await Popovers.InvokeAsync(() => Popovers.FindAll(".mud-list-item")[0].Click()).WaitAsync(Timeout, Ct);

        cut.WaitForAssertion(() => Assert.Contains("Eintrag auf der Liste: Butter", cut.Markup, StringComparison.Ordinal), Timeout);
        Assert.Equal("Anderes Produkt für „Butter“", RowButton(cut, "Butter, irisch", "Anderes Produkt").GetAttribute("aria-label"));
    }
}
