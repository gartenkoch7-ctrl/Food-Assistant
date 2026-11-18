using Bunit;
using FoodAssistant.Contracts.Common;
using FoodAssistant.Contracts.Inventory;
using FoodAssistant.UI.Checkout;
using MudBlazor;

namespace FoodAssistant.UI.Tests.Checkout;

public sealed class CheckoutLineEditorTests : CheckoutTestContext
{
    private async Task<IDialogReference> EditAsync(CheckoutLine line) =>
        await ShowAsync<CheckoutLineEditor>(p => { p.Add(d => d.Line, line); p.Add(d => d.Locations, Locations); });

    [Fact]
    public async Task RuleDate_IsInformation_KeptWhenNothingIsEntered()
    {
        var line = new CheckoutLine(AddEntry("Erbsen", Guid.CreateVersion7(), Freezer.Id, Today.AddDays(90), ExpiryKindDto.UseBy, fromRule: true));
        var dialog = await EditAsync(line);
        Dialogs.WaitForAssertion(() => Assert.Contains("Datum aus Regel „gefroren“: 16.02.2027", Dialogs.Markup, StringComparison.Ordinal), Timeout);
        Assert.Contains("Selbst eingefroren", Dialogs.Markup, StringComparison.Ordinal);

        await Dialogs.InvokeAsync(() => ButtonByText(Dialogs, "Speichern").Click()).WaitAsync(Timeout, Ct);

        var edited = Assert.IsType<CheckoutLine>((await dialog.Result.WaitAsync(Timeout, Ct))!.Data);
        Assert.Equal((true, (DateOnly?)Today.AddDays(90), (Guid?)Freezer.Id), (edited.FromRule, edited.Date, edited.LocationId));
        Assert.Null(edited.ToRequest().ExpiresOn);
        Assert.True(line.FromRule);
    }

    [Fact]
    public async Task NewDate_NeedsTheKind_NoPreselection_ThenReplacesTheRuleDate()
    {
        var line = new CheckoutLine(AddEntry("Erbsen", Guid.CreateVersion7(), Freezer.Id, Today.AddDays(90), ExpiryKindDto.UseBy, fromRule: true));
        var dialog = await EditAsync(line);
        Dialogs.WaitForAssertion(() => Assert.Contains("Ablaufdatum", Dialogs.Markup, StringComparison.Ordinal), Timeout);

        await Dialogs.InvokeAsync(() => Dialogs.Find(".fa-date-field input").Change("24.11.2026")).WaitAsync(Timeout, Ct);
        Dialogs.WaitForAssertion(() => Assert.Contains("Art des Datums", Dialogs.Markup, StringComparison.Ordinal), Timeout);
        Assert.DoesNotContain(Dialogs.FindAll("label.fa-option input"), i => i.HasAttribute("checked"));
        await Dialogs.InvokeAsync(() => ButtonByText(Dialogs, "Speichern").Click()).WaitAsync(Timeout, Ct);
        Assert.Contains("Bitte ausfüllen.", Dialogs.Markup, StringComparison.Ordinal);
        Assert.Contains("gekaufte Tiefkühlware", Dialogs.Markup, StringComparison.Ordinal);

        await Dialogs.InvokeAsync(() => Dialogs.FindAll("label.fa-option input")[0].Change(true)).WaitAsync(Timeout, Ct);
        await Dialogs.InvokeAsync(() => ButtonByText(Dialogs, "Speichern").Click()).WaitAsync(Timeout, Ct);

        var edited = Assert.IsType<CheckoutLine>((await dialog.Result.WaitAsync(Timeout, Ct))!.Data);
        var request = edited.ToRequest();
        Assert.Equal((new DateOnly(2026, 11, 24), ExpiryKindDto.BestBefore, false), (request.ExpiresOn!.Value, request.ExpiryKind, request.SelfFrozen));
        Assert.False(edited.FromRule);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SuggestedDate_GoesWhenTheDialogMovesItOutOfTheFreezer(bool viaRowLater)
    {
        var line = new CheckoutLine(AddEntry("Spinat", Guid.CreateVersion7(), Freezer.Id, Today.AddDays(300), ExpiryKindDto.BestBefore));
        var dialog = await EditAsync(line);
        Dialogs.WaitForAssertion(() => Assert.Contains("Lagerort", Dialogs.Markup, StringComparison.Ordinal), Timeout);

        if (!viaRowLater)
        {
            await Dialogs.InvokeAsync(() => Dialogs.FindComponent<MudSelect<Guid?>>().Instance.ValueChanged.InvokeAsync(Fridge.Id)).WaitAsync(Timeout, Ct);
            Assert.Empty(Dialogs.FindAll("label.fa-option input"));
        }

        await Dialogs.InvokeAsync(() => ButtonByText(Dialogs, "Speichern").Click()).WaitAsync(Timeout, Ct);
        var edited = Assert.IsType<CheckoutLine>((await dialog.Result.WaitAsync(Timeout, Ct))!.Data);
        if (viaRowLater)
        {
            // Saved untouched, the date stays a suggestion; a later move in the row still drops it.
            edited.MoveTo(Fridge.Id, freezer: false, fromFreezer: true);
        }

        Assert.Equal(((Guid?)Fridge.Id, (DateOnly?)null, ExpiryKindDto.None), (edited.LocationId, edited.Date, edited.Kind));
    }

    [Fact]
    public async Task DroppedSuggestion_TypedAgain_CountsAsThePersonsOwnDate()
    {
        var line = new CheckoutLine(AddEntry("Milch", Guid.CreateVersion7(), Fridge.Id, Today.AddDays(7), ExpiryKindDto.BestBefore));
        var dialog = await EditAsync(line);
        Dialogs.WaitForAssertion(() => Assert.Contains("Lagerort", Dialogs.Markup, StringComparison.Ordinal), Timeout);

        await Dialogs.InvokeAsync(() => Dialogs.FindComponent<MudSelect<Guid?>>().Instance.ValueChanged.InvokeAsync(Freezer.Id)).WaitAsync(Timeout, Ct);
        await Dialogs.InvokeAsync(() => Dialogs.Find(".fa-date-field input").Change("25.11.2026")).WaitAsync(Timeout, Ct);
        await Dialogs.InvokeAsync(() => Dialogs.FindAll("label.fa-option input")[0].Change(true)).WaitAsync(Timeout, Ct);
        await Dialogs.InvokeAsync(() => ButtonByText(Dialogs, "Speichern").Click()).WaitAsync(Timeout, Ct);

        var edited = Assert.IsType<CheckoutLine>((await dialog.Result.WaitAsync(Timeout, Ct))!.Data);
        var request = edited.ToRequest();
        Assert.Equal(((Guid?)Freezer.Id, (DateOnly?)Today.AddDays(7), ExpiryKindDto.BestBefore, false), (request.StorageLocationId, request.ExpiresOn, request.ExpiryKind, edited.Suggested));
    }

    [Fact]
    public async Task SuggestedDateReplaced_TheKindIsNoLongerPreselected()
    {
        var line = new CheckoutLine(AddEntry("Milch", Guid.CreateVersion7(), Fridge.Id, Today.AddDays(7), ExpiryKindDto.BestBefore));
        await EditAsync(line);
        Dialogs.WaitForAssertion(() => Assert.Contains(Dialogs.FindAll("label.fa-option input"), i => i.HasAttribute("checked")), Timeout);

        await Dialogs.InvokeAsync(() => Dialogs.Find(".fa-date-field input").Change("30.11.2026")).WaitAsync(Timeout, Ct);
        Assert.DoesNotContain(Dialogs.FindAll("label.fa-option input"), i => i.HasAttribute("checked"));
        await Dialogs.InvokeAsync(() => ButtonByText(Dialogs, "Speichern").Click()).WaitAsync(Timeout, Ct);

        Assert.Contains("Bitte ausfüllen.", Dialogs.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Fridge_HasNoFreezerSwitch_AmountIsTakenOver()
    {
        var line = new CheckoutLine(AddEntry("Milch", Guid.CreateVersion7(), Fridge.Id, Today.AddDays(7), ExpiryKindDto.BestBefore));
        var dialog = await EditAsync(line);
        Dialogs.WaitForAssertion(() => Assert.Contains("Lagerort", Dialogs.Markup, StringComparison.Ordinal), Timeout);
        Assert.DoesNotContain("Selbst eingefroren", Dialogs.Markup, StringComparison.Ordinal);

        await Dialogs.InvokeAsync(() => Dialogs.Find(".fa-quantity-input input").Change("2")).WaitAsync(Timeout, Ct);
        await Dialogs.InvokeAsync(() => ButtonByText(Dialogs, "Speichern").Click()).WaitAsync(Timeout, Ct);

        var edited = Assert.IsType<CheckoutLine>((await dialog.Result.WaitAsync(Timeout, Ct))!.Data);
        Assert.Equal(new QuantityDto(2m, "l"), edited.Quantity);
        Assert.Equal((Today.AddDays(7), ExpiryKindDto.BestBefore), (edited.Date, edited.Kind));
        Assert.Equal(new QuantityDto(1m, "l"), line.Quantity);
    }
}
