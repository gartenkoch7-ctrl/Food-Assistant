using Bunit;
using FoodAssistant.Contracts.Shopping;
using FoodAssistant.UI.Shopping;
using MudBlazor;

namespace FoodAssistant.UI.Tests.Shopping;

/// <summary>
/// What the dialog does with the conflict outcome of the server. On a change the incoming state wins in every
/// case but one: "checked wins" for an unchecked request. A delete only applies without a conflict.
/// </summary>
public sealed class ShoppingItemDialogOutcomeTests : ShoppingTestContext
{
    private async Task<IDialogReference> OpenAsync(ShoppingItemDto item, ShoppingListDto list) =>
        await ShowAsync<ShoppingItemDialog>(p =>
        {
            p.Add(d => d.Item, item);
            p.Add(d => d.Lists, [list]);
        });

    [Fact]
    public async Task SaveLostToACheckOff_StaysOpen_SaysSo_AndShowsTheKeptState()
    {
        var list = AddList();
        var stale = AddItem(list.Id, "Milch", "dairy_chilled");
        // Meanwhile someone checked the item: the server keeps its checked state and its note.
        Shopping.Items[0] = stale with { Checked = true, Note = "vom Server", Version = stale.Version + 2 };
        Shopping.ChangeOutcome = ShoppingConflictOutcomeDto.CheckedWins;
        var dialog = await OpenAsync(stale, list);

        InputFor(Dialogs, "Notiz").Change("meine Änderung");
        ButtonByText(Dialogs, "Speichern").Click();

        Dialogs.WaitForAssertion(() => Assert.Contains("Deine Änderung wurde nicht übernommen", Dialogs.Markup, StringComparison.Ordinal));
        Assert.False(dialog.Result.IsCompleted);
        Assert.Equal("vom Server", InputFor(Dialogs, "Notiz").GetAttribute("value"));
    }

    [Theory]
    [InlineData(ShoppingConflictOutcomeDto.NoConflict)]
    [InlineData(ShoppingConflictOutcomeDto.LastWriteWins)]
    [InlineData(ShoppingConflictOutcomeDto.ChangeWinsOverDelete)]
    [InlineData(ShoppingConflictOutcomeDto.CheckedWinsOverDelete)]
    public async Task SaveThatTheServerApplied_Closes(ShoppingConflictOutcomeDto outcome)
    {
        var list = AddList();
        var item = AddItem(list.Id, "Milch", "dairy_chilled");
        Shopping.ChangeOutcome = outcome;
        var dialog = await OpenAsync(item, list);

        InputFor(Dialogs, "Notiz").Change("neu");
        ButtonByText(Dialogs, "Speichern").Click();

        var result = await dialog.Result.WaitAsync(TimeSpan.FromSeconds(10), Xunit.TestContext.Current.CancellationToken);
        Assert.False(result!.Canceled);
        Assert.Equal("neu", Shopping.Items[0].Note);
    }

    [Fact]
    public async Task CheckedWins_ForACheckedItem_MeansTheSaveWasApplied()
    {
        var list = AddList();
        var item = AddItem(list.Id, "Milch", "dairy_chilled", isChecked: true);
        Shopping.ChangeOutcome = ShoppingConflictOutcomeDto.CheckedWins;
        var dialog = await OpenAsync(item, list);

        InputFor(Dialogs, "Notiz").Change("neu");
        ButtonByText(Dialogs, "Speichern").Click();

        var result = await dialog.Result.WaitAsync(TimeSpan.FromSeconds(10), Xunit.TestContext.Current.CancellationToken);
        Assert.False(result!.Canceled);
        Assert.Equal("neu", Shopping.Items[0].Note);
    }

    [Fact]
    public async Task Move_WithUnsavedChanges_AsksToSaveFirst_AndMovesNothing()
    {
        var home = AddList();
        var other = AddList("Wochenende", isDefault: false);
        var item = AddItem(home.Id, "Milch");
        var dialog = await ShowAsync<ShoppingItemDialog>(p =>
        {
            p.Add(d => d.Item, item);
            p.Add(d => d.Lists, [home, other]);
        });

        InputFor(Dialogs, "Notiz").Change("noch nicht gespeichert");
        Dialogs.FindAll(".mud-select .mud-input-control")[2].MouseDown();
        Popovers.WaitForAssertion(() => Assert.NotEmpty(Popovers.FindAll(".mud-list-item")));
        Popovers.FindAll(".mud-list-item").First(i => i.TextContent.Contains("Wochenende", StringComparison.Ordinal)).Click();
        ButtonByText(Dialogs, "Verschieben").Click();

        Dialogs.WaitForAssertion(() => Assert.Contains("ungespeicherte Änderungen", Dialogs.Markup, StringComparison.Ordinal));
        Assert.False(dialog.Result.IsCompleted);
        Assert.Equal(home.Id, Shopping.Items[0].ListId);
    }

    [Fact]
    public async Task LostDelete_StaysOpen_AndSaysTheItemWasNotDeleted()
    {
        var list = AddList();
        var item = AddItem(list.Id, "Milch");
        var dialog = await OpenAsync(item, list);
        Shopping.DeleteOutcome = ShoppingConflictOutcomeDto.ChangeWinsOverDelete;

        ButtonByText(Dialogs, "Eintrag löschen").Click();
        Dialogs.WaitForAssertion(() => Dialogs.Find(".mud-message-box__yes-button"));
        Dialogs.Find(".mud-message-box__yes-button").Click();

        Dialogs.WaitForAssertion(() => Assert.Contains("wurde nicht gelöscht", Dialogs.Markup, StringComparison.Ordinal));
        Assert.False(dialog.Result.IsCompleted);
        Assert.Single(Shopping.Items);
    }

    [Fact]
    public async Task AlreadyDeleted_CountsAsDeleted()
    {
        var list = AddList();
        var item = AddItem(list.Id, "Milch");
        var dialog = await OpenAsync(item, list);
        Shopping.DeleteOutcome = ShoppingConflictOutcomeDto.BothDeleted;

        ButtonByText(Dialogs, "Eintrag löschen").Click();
        Dialogs.WaitForAssertion(() => Dialogs.Find(".mud-message-box__yes-button"));
        Dialogs.Find(".mud-message-box__yes-button").Click();

        var result = await dialog.Result.WaitAsync(TimeSpan.FromSeconds(10), Xunit.TestContext.Current.CancellationToken);
        Assert.False(result!.Canceled);
    }

    [Fact]
    public async Task LostSave_UsesTheEnglishNotice()
    {
        UseCulture("en");
        var list = AddList();
        var stale = AddItem(list.Id, "Milk");
        Shopping.Items[0] = stale with { Checked = true, Version = stale.Version + 1 };
        Shopping.ChangeOutcome = ShoppingConflictOutcomeDto.CheckedWins;
        await OpenAsync(stale, list);

        ButtonByText(Dialogs, "Save").Click();

        Dialogs.WaitForAssertion(() => Assert.Contains("Your change was not applied", Dialogs.Markup, StringComparison.Ordinal));
    }
}
