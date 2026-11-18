using Bunit;
using FoodAssistant.Contracts.Households;
using FoodAssistant.Contracts.Shopping;
using FoodAssistant.Contracts.Sync;
using FoodAssistant.UI.Shopping;
using FoodAssistant.UI.Tests.Support;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace FoodAssistant.UI.Tests.Shopping;

public sealed class ShoppingPageTests : ShoppingTestContext
{
    private string CurrentUri => Services.GetRequiredService<NavigationManager>().Uri;

    [Fact]
    public void ExactlyOneList_OpensItDirectly()
    {
        var list = AddList();

        Render<ShoppingPage>();

        Assert.EndsWith($"/shopping/{list.Id:D}", CurrentUri, StringComparison.Ordinal);
    }

    [Fact]
    public void SeveralLists_ShowTheOverview_WithNameStoreAndOpenCount()
    {
        var store = AddStore("Markt");
        var standard = AddList();
        var weekend = AddList("Wochenende", isDefault: false, storeId: store.Id);
        AddItem(standard.Id, "Milch");
        AddItem(weekend.Id, "Brot");
        AddItem(weekend.Id, "Wein");
        AddItem(weekend.Id, "Käse", isChecked: true);

        var cut = Render<ShoppingPage>();

        Assert.Equal("Einkauf", cut.Find("h1").TextContent);
        var rows = cut.FindAll("li.fa-list-row");
        Assert.Equal(2, rows.Count);
        Assert.Contains("Einkaufsliste", rows[0].TextContent, StringComparison.Ordinal);
        Assert.Contains("1 offen", rows[0].TextContent, StringComparison.Ordinal);
        Assert.Contains("Wochenende", rows[1].TextContent, StringComparison.Ordinal);
        Assert.Contains("Laden: Markt", rows[1].TextContent, StringComparison.Ordinal);
        Assert.Contains("2 offen", rows[1].TextContent, StringComparison.Ordinal);
        Assert.Equal($"/shopping/{weekend.Id:D}", rows[1].QuerySelector("a")!.GetAttribute("href"));
    }

    [Fact]
    public void TheOverview_UsesEnglishTexts()
    {
        UseCulture("en");
        AddList();
        AddList("Weekend", isDefault: false);

        var cut = Render<ShoppingPage>();

        Assert.Equal("Shopping", cut.Find("h1").TextContent);
        Assert.Contains("Shopping list", cut.Markup, StringComparison.Ordinal);
        Assert.NotNull(cut.Find("a[aria-label='Open Weekend']"));
    }

    [Fact]
    public void CreateList_AddsItWithTheChosenStore()
    {
        AddList();
        AddList("Wochenende", isDefault: false);
        var store = AddStore("Markt");
        var cut = Render<ShoppingPage>();

        cut.FindAll("button").Last(b => b.TextContent.Contains("Liste anlegen", StringComparison.Ordinal)).Click();
        Dialogs.WaitForAssertion(() => Assert.Contains("Neue Liste", Dialogs.Markup, StringComparison.Ordinal));
        InputFor(Dialogs, "Name").Change("Party");
        Dialogs.Find(".mud-select .mud-input-control").MouseDown();
        Popovers.WaitForAssertion(() => Assert.NotEmpty(Popovers.FindAll(".mud-list-item")));
        Popovers.FindAll(".mud-list-item").First(i => i.TextContent.Contains("Markt", StringComparison.Ordinal)).Click();
        ButtonByText(Dialogs, "Speichern").Click();

        cut.WaitForAssertion(() => Assert.Equal(3, cut.FindAll("li.fa-list-row").Count));
        Assert.Contains(Shopping.Lists, l => l is { Name: "Party", IsDefault: false } && l.StoreId == store.Id);
    }

    [Fact]
    public void CreateList_AsDefault_MakesItTheOnlyDefault()
    {
        AddList();
        AddList("Wochenende", isDefault: false);
        var cut = Render<ShoppingPage>();

        cut.FindAll("button").Last(b => b.TextContent.Contains("Liste anlegen", StringComparison.Ordinal)).Click();
        Dialogs.WaitForAssertion(() => Assert.Contains("Standardliste", Dialogs.Markup, StringComparison.Ordinal));
        InputFor(Dialogs, "Name").Change("Neu");
        Dialogs.Find("input[type=checkbox]").Change(true);
        ButtonByText(Dialogs, "Speichern").Click();

        cut.WaitForAssertion(() => Assert.Equal(3, cut.FindAll("li.fa-list-row").Count));
        Assert.Equal(["Neu"], Shopping.Lists.Where(l => l.IsDefault).Select(l => l.Name));
    }

    [Fact]
    public void ListDialog_RefusesAnEmptyName()
    {
        AddList();
        AddList("Wochenende", isDefault: false);
        var cut = Render<ShoppingPage>();
        cut.FindAll("button").Last(b => b.TextContent.Contains("Liste anlegen", StringComparison.Ordinal)).Click();
        Dialogs.WaitForAssertion(() => Assert.Contains("Neue Liste", Dialogs.Markup, StringComparison.Ordinal));

        ButtonByText(Dialogs, "Speichern").Click();

        Assert.Contains("Pflichtfeld", Dialogs.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("create-list", Shopping.Calls);
    }

    [Fact]
    public void Edit_ChangesTheNameWithTheEtag()
    {
        AddList();
        var weekend = AddList("Wochenende", isDefault: false);
        var cut = Render<ShoppingPage>();

        cut.Find("button[aria-label='Wochenende bearbeiten']").Click();
        Dialogs.WaitForAssertion(() => Assert.Contains("Liste bearbeiten", Dialogs.Markup, StringComparison.Ordinal));
        InputFor(Dialogs, "Name").Change("Feier");
        ButtonByText(Dialogs, "Speichern").Click();

        cut.WaitForAssertion(() => Assert.Contains("Feier", cut.Markup, StringComparison.Ordinal));
        Assert.Equal("Feier", Shopping.Lists.Single(l => l.Id == weekend.Id).Name);
    }

    [Fact]
    public void Edit_AfterSomeoneElseChangedTheList_ReloadsInsteadOfOverwriting()
    {
        AddList();
        var weekend = AddList("Wochenende", isDefault: false);
        var cut = Render<ShoppingPage>();
        cut.Find("button[aria-label='Wochenende bearbeiten']").Click();
        Dialogs.WaitForAssertion(() => Assert.Contains("Liste bearbeiten", Dialogs.Markup, StringComparison.Ordinal));
        Shopping.Lists[1] = weekend with { Name = "Von anderen", Version = 2 };

        ButtonByText(Dialogs, "Speichern").Click();

        Dialogs.WaitForAssertion(() => Assert.Contains("inzwischen geändert", Dialogs.Markup, StringComparison.Ordinal));
        Assert.Equal("Von anderen", InputFor(Dialogs, "Name").GetAttribute("value"));
        Assert.Equal("Von anderen", Shopping.Lists[1].Name);
    }

    [Fact]
    public async Task Resync_ReloadsTheOverview()
    {
        AddList();
        var weekend = AddList("Wochenende", isDefault: false);
        var cut = Render<ShoppingPage>();

        AddItem(weekend.Id, "Brot");
        await cut.InvokeAsync(() => Live.RaiseResyncedAsync());

        cut.WaitForAssertion(() => Assert.Contains("1 offen", cut.FindAll("li.fa-list-row")[1].TextContent, StringComparison.Ordinal));
    }

    [Fact]
    public void FailedLoad_ShowsTheError_NotTheEmptyState()
    {
        Shopping.FailWith = FakeAccountClient.Error(0, FoodAssistant.Client.Http.ClientErrorCodes.Network);

        var cut = Render<ShoppingPage>();

        Assert.NotNull(cut.Find("[role=alert]"));
        Assert.DoesNotContain("leer", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Delete_AsksForConfirmation_AndTheDefaultListHasNoDeleteButton()
    {
        AddList();
        AddList("Wochenende", isDefault: false);
        var cut = Render<ShoppingPage>();

        Assert.Empty(cut.FindAll("button[aria-label='Einkaufsliste löschen']"));
        cut.Find("button[aria-label='Wochenende löschen']").Click();
        Dialogs.WaitForAssertion(() => Assert.Contains("Liste löschen?", Dialogs.Markup, StringComparison.Ordinal));
        Dialogs.Find(".mud-message-box__yes-button").Click();

        // One list is left, so the page opens it directly.
        cut.WaitForAssertion(() => Assert.Single(Shopping.Lists));
        Assert.EndsWith($"/shopping/{Shopping.Lists[0].Id:D}", CurrentUri, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LiveChange_ReloadsTheOverview()
    {
        AddList();
        var weekend = AddList("Wochenende", isDefault: false);
        var cut = Render<ShoppingPage>();

        AddItem(weekend.Id, "Brot");
        await cut.InvokeAsync(() => Live.RaiseChangedAsync(new HouseholdChangesMessage(
            FakeAccountClient.TestHouseholdId, [new HouseholdChangeDto(SyncEntityTypes.ShoppingItem, Guid.CreateVersion7(), 5, false)])));

        cut.WaitForAssertion(() => Assert.Contains("1 offen", cut.FindAll("li.fa-list-row")[1].TextContent, StringComparison.Ordinal));
    }

}

public sealed class ShoppingPageRestrictedTests() : ShoppingTestContext(role: HouseholdRole.Restricted)
{
    [Fact]
    public void RestrictedMember_SeesTheLists_ButCannotCreateEditOrDelete()
    {
        AddList();
        AddList("Wochenende", isDefault: false);

        var cut = Render<ShoppingPage>();

        Assert.Equal(2, cut.FindAll("li.fa-list-row").Count);
        Assert.Empty(cut.FindAll("button"));
        Assert.Equal(2, cut.FindAll("a").Count);
    }
}
