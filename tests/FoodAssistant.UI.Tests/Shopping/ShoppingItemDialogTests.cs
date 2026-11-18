using Bunit;
using FoodAssistant.Contracts.Common;
using FoodAssistant.Contracts.Households;
using FoodAssistant.Contracts.Shopping;
using FoodAssistant.UI.Shopping;
using FoodAssistant.UI.Tests.Support;
using MudBlazor;

namespace FoodAssistant.UI.Tests.Shopping;

public sealed class ShoppingItemDialogTests : ShoppingTestContext
{
    private async Task<IDialogReference> OpenAsync(ShoppingItemDto item, params ShoppingListDto[] lists) =>
        await ShowAsync<ShoppingItemDialog>(p =>
        {
            p.Add(d => d.Item, item);
            p.Add(d => d.Lists, lists);
        });

    private void PickFrom(int selectIndex, string text)
    {
        Dialogs.FindAll(".mud-select .mud-input-control")[selectIndex].MouseDown();
        Popovers.WaitForAssertion(() => Assert.NotEmpty(Popovers.FindAll(".mud-list-item")));
        Popovers.FindAll(".mud-list-item").First(i => i.TextContent.Contains(text, StringComparison.Ordinal)).Click();
    }

    [Fact]
    public async Task Save_SendsTheFullState_WithTheBaseVersion()
    {
        var list = AddList();
        var item = AddItem(list.Id, "Milch", "dairy_chilled", isChecked: false, quantity: new QuantityDto(1, "l"));
        var dialog = await OpenAsync(item, list);

        InputFor(Dialogs, "Notiz").Change("laktosefrei");
        InputFor(Dialogs, "Bezeichnung").Change("Hafermilch");
        PickFrom(1, "Getränke");
        ButtonByText(Dialogs, "Speichern").Click();

        var result = await dialog.Result.WaitAsync(TimeSpan.FromSeconds(10), Xunit.TestContext.Current.CancellationToken);
        Assert.False(result!.Canceled);
        var request = Assert.Single(Shopping.Changes);
        Assert.Equal(new ChangeShoppingItemRequest(item.Version, false, new QuantityDto(1, "l"), "laktosefrei", "Hafermilch", "beverages"), request);
    }

    [Fact]
    public async Task Save_WithANewAmount_KeepsTheUnit()
    {
        var list = AddList();
        var item = AddItem(list.Id, "Milch", "dairy_chilled", quantity: new QuantityDto(1, "l"));
        var dialog = await OpenAsync(item, list);

        InputFor(Dialogs, "Menge").Change("3");
        ButtonByText(Dialogs, "Speichern").Click();

        await dialog.Result.WaitAsync(TimeSpan.FromSeconds(10), Xunit.TestContext.Current.CancellationToken);
        Assert.Equal(new QuantityDto(3m, "l"), Shopping.Changes[0].Quantity);
    }

    [Fact]
    public async Task ProductItem_KeepsItsName()
    {
        var list = AddList();
        var item = AddItem(list.Id, "Vollmilch", "dairy_chilled", productId: Guid.CreateVersion7());
        await OpenAsync(item, list);

        Assert.True(InputFor(Dialogs, "Bezeichnung").HasAttribute("readonly"));
    }

    [Fact]
    public async Task EmptyText_IsRefused_WithoutCallingTheServer()
    {
        var list = AddList();
        var item = AddItem(list.Id, "Milch");
        await OpenAsync(item, list);

        InputFor(Dialogs, "Bezeichnung").Change(string.Empty);
        ButtonByText(Dialogs, "Speichern").Click();

        Assert.Contains("mud-input-error", Dialogs.Find("input[required]").ParentElement!.ClassName, StringComparison.Ordinal);
        Assert.Empty(Shopping.Changes);
    }

    [Fact]
    public async Task ServerRefusal_IsShownInTheDialog_AndTheDialogStaysOpen()
    {
        var list = AddList();
        var item = AddItem(list.Id, "Milch");
        var dialog = await OpenAsync(item, list);
        Shopping.FailChangeWith = FakeAccountClient.Error(409, "shopping.unknown_version");

        ButtonByText(Dialogs, "Speichern").Click();

        Dialogs.WaitForAssertion(() => Assert.Contains("nicht auf dem aktuellen Stand", Dialogs.Find("[role=alert]").TextContent, StringComparison.Ordinal));
        Assert.False(dialog.Result.IsCompleted);
    }

    [Fact]
    public async Task Move_OffersTheOtherLists_AndMovesTheItem()
    {
        var home = AddList();
        var weekend = AddList("Wochenende", isDefault: false);
        var item = AddItem(home.Id, "Milch");
        var dialog = await OpenAsync(item, home, weekend);

        // The only target is the other list: the item's own list is not offered.
        PickFrom(2, "Wochenende");
        ButtonByText(Dialogs, "Verschieben").Click();

        var result = await dialog.Result.WaitAsync(TimeSpan.FromSeconds(10), Xunit.TestContext.Current.CancellationToken);
        Assert.False(result!.Canceled);
        Assert.Equal(weekend.Id, Shopping.Items[0].ListId);
    }

    [Fact]
    public async Task Move_IsNotOffered_WithASingleList()
    {
        var list = AddList();
        var item = AddItem(list.Id, "Milch");
        await OpenAsync(item, list);

        Assert.DoesNotContain("In andere Liste verschieben", Dialogs.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Delete_AsksForConfirmation_ThenSendsTheBaseVersion()
    {
        var list = AddList();
        var item = AddItem(list.Id, "Milch");
        var dialog = await OpenAsync(item, list);

        ButtonByText(Dialogs, "Eintrag löschen").Click();
        Dialogs.WaitForAssertion(() => Assert.Contains("Eintrag löschen?", Dialogs.Markup, StringComparison.Ordinal));
        Dialogs.Find(".mud-message-box__yes-button").Click();

        var result = await dialog.Result.WaitAsync(TimeSpan.FromSeconds(10), Xunit.TestContext.Current.CancellationToken);
        Assert.False(result!.Canceled);
        Assert.Empty(Shopping.Items);
        Assert.Contains($"delete-item:{item.Id}:{item.Version}", Shopping.Calls);
    }

    [Fact]
    public async Task Delete_Declined_ChangesNothing()
    {
        var list = AddList();
        var item = AddItem(list.Id, "Milch");
        var dialog = await OpenAsync(item, list);

        ButtonByText(Dialogs, "Eintrag löschen").Click();
        Dialogs.WaitForAssertion(() => Dialogs.Find(".mud-message-box__yes-button"));
        Dialogs.FindAll("button").Last(b => b.TextContent.Trim() == "Abbrechen").Click();

        Assert.Single(Shopping.Items);
        Assert.False(dialog.Result.IsCompleted);
    }

    [Fact]
    public async Task English_UsesEnglishLabels()
    {
        UseCulture("en");
        var list = AddList();
        var item = AddItem(list.Id, "Milk");
        await OpenAsync(item, list);

        Assert.Contains("Quantity", Dialogs.Markup, StringComparison.Ordinal);
        Assert.Contains("Delete item", Dialogs.Markup, StringComparison.Ordinal);
    }
}

public sealed class ShoppingItemDialogRestrictedTests() : ShoppingTestContext(role: HouseholdRole.Restricted)
{
    [Fact]
    public async Task RestrictedMember_SeesTheItemReadOnly()
    {
        var list = AddList();
        var item = AddItem(list.Id, "Milch", "dairy_chilled", quantity: new QuantityDto(2, "l"), note: "bio");
        var dialog = await ShowAsync<ShoppingItemDialog>(p =>
        {
            p.Add(d => d.Item, item);
            p.Add(d => d.Lists, [list]);
        });

        Assert.Contains("nicht ändern", Dialogs.Markup, StringComparison.Ordinal);
        Assert.Contains("2 l", Dialogs.Markup, StringComparison.Ordinal);
        Assert.Contains("Kühlregal", Dialogs.Markup, StringComparison.Ordinal);
        Assert.Empty(Dialogs.FindAll("input"));
        Assert.DoesNotContain(Dialogs.FindAll("button"), b => b.TextContent.Contains("Speichern", StringComparison.Ordinal));
        Assert.DoesNotContain(Dialogs.FindAll("button"), b => b.TextContent.Contains("löschen", StringComparison.Ordinal));

        ButtonByTextClose(Dialogs);
        var result = await dialog.Result.WaitAsync(TimeSpan.FromSeconds(10), Xunit.TestContext.Current.CancellationToken);
        Assert.True(result!.Canceled);
        Assert.Empty(Shopping.Changes);
    }

    private static void ButtonByTextClose(IRenderedComponent<MudDialogProvider> provider) =>
        provider.FindAll("button").First(b => b.TextContent.Trim() == "Schließen").Click();
}
