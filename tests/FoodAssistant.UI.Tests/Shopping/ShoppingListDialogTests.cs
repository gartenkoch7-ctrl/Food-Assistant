using Bunit;
using FoodAssistant.UI.Shopping;
using FoodAssistant.UI.Tests.Support;

namespace FoodAssistant.UI.Tests.Shopping;

public sealed class ShoppingListDialogTests : ShoppingTestContext
{
    [Fact]
    public async Task FailingDefaultUpdate_AfterTheCreate_DoesNotCreateAnotherListOnTheNextSave()
    {
        AddList();
        var dialog = await ShowAsync<ShoppingListDialog>();
        Shopping.FailUpdateListWith = FakeAccountClient.Error(500, "common.internal_error");

        InputFor(Dialogs, "Name").Change("Party");
        Dialogs.Find("input[type=checkbox]").Change(true);
        ButtonByText(Dialogs, "Speichern").Click();

        Dialogs.WaitForAssertion(() => Assert.NotNull(Dialogs.Find("[role=alert]")));
        Assert.Equal(2, Shopping.Lists.Count);
        Assert.False(dialog.Result.IsCompleted);

        Shopping.FailUpdateListWith = null;
        ButtonByText(Dialogs, "Speichern").Click();

        var result = await dialog.Result.WaitAsync(TimeSpan.FromSeconds(10), Xunit.TestContext.Current.CancellationToken);
        Assert.False(result!.Canceled);
        Assert.Equal(2, Shopping.Lists.Count);
        Assert.Equal(["Party"], Shopping.Lists.Where(l => l.IsDefault).Select(l => l.Name));
        Assert.Single(Shopping.Calls, c => c == "create-list");
    }

    [Fact]
    public async Task ConflictOnCreate_ThatMeansTheListAlreadyExists_TakesThatListInsteadOfFailing()
    {
        AddList();
        var dialog = await ShowAsync<ShoppingListDialog>();
        Shopping.CreateConflictOnce = true;

        InputFor(Dialogs, "Name").Change("Party");
        ButtonByText(Dialogs, "Speichern").Click();

        var result = await dialog.Result.WaitAsync(TimeSpan.FromSeconds(10), Xunit.TestContext.Current.CancellationToken);
        Assert.False(result!.Canceled);
        Assert.Equal(2, Shopping.Lists.Count);
        Assert.Single(Shopping.Calls, c => c == "create-list");
    }

    [Fact]
    public async Task RecoveredList_WithAnotherName_IsUpdatedToWhatIsTyped()
    {
        AddList();
        var dialog = await ShowAsync<ShoppingListDialog>();
        Shopping.CreateConflictOnce = true;
        Shopping.ConflictedName = "Alt";

        InputFor(Dialogs, "Name").Change("Party");
        ButtonByText(Dialogs, "Speichern").Click();

        await dialog.Result.WaitAsync(TimeSpan.FromSeconds(10), Xunit.TestContext.Current.CancellationToken);
        Assert.Contains(Shopping.Calls, c => c.StartsWith("update-list:", StringComparison.Ordinal));
        Assert.Equal("Party", Shopping.Lists.Last().Name);
    }

    [Fact]
    public async Task DeletedStore_AsksForANewChoice_AndReadsTheStoresAgain()
    {
        AddList();
        var store = AddStore("Markt");
        await ShowAsync<ShoppingListDialog>();
        Dialogs.FindAll(".mud-select .mud-input-control")[0].MouseDown();
        Popovers.WaitForAssertion(() => Assert.NotEmpty(Popovers.FindAll(".mud-list-item")));
        Popovers.FindAll(".mud-list-item").First(i => i.TextContent.Contains("Markt", StringComparison.Ordinal)).Click();
        InputFor(Dialogs, "Name").Change("Party");
        Stores.Stores.Remove(store);
        Shopping.FailWith = new FoodAssistant.Client.Http.ApiError(
            400, "shopping.validation_failed", null, new Dictionary<string, string[]> { ["storeId"] = ["not_found"] }, null);

        ButtonByText(Dialogs, "Speichern").Click();

        Dialogs.WaitForAssertion(() => Assert.Contains("inzwischen gelöscht", Dialogs.Markup, StringComparison.Ordinal));
        Assert.Contains("Kein Laden", Dialogs.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheDefaultHint_IsTiedToTheSwitch()
    {
        AddList();
        await ShowAsync<ShoppingListDialog>();

        var describedBy = Dialogs.Find("input[type=checkbox]").ParentElement!.GetAttribute("aria-describedby")
            ?? Dialogs.Find("[aria-describedby]").GetAttribute("aria-describedby");

        Assert.Equal("fa-list-default-hint", describedBy);
        Assert.NotNull(Dialogs.Find("#fa-list-default-hint"));
    }
}
