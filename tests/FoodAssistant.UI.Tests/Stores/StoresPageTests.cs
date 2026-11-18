using Bunit;
using FoodAssistant.Contracts.Households;
using FoodAssistant.Contracts.Sync;
using FoodAssistant.UI.Stores;
using FoodAssistant.UI.Tests.Shopping;
using FoodAssistant.UI.Tests.Support;

namespace FoodAssistant.UI.Tests.Stores;

public sealed class StoresPageTests : ShoppingTestContext
{
    [Fact]
    public void NoStores_ShowTheEmptyStateWithACreateButton()
    {
        var cut = Render<StoresPage>();

        Assert.Equal("Läden", cut.Find("h1").TextContent);
        Assert.Contains("Noch keine Läden", cut.Markup, StringComparison.Ordinal);
        Assert.Contains(cut.FindAll("button"), b => b.TextContent.Contains("Laden anlegen", StringComparison.Ordinal));
    }

    [Fact]
    public void Stores_AreListed_WithAccessibleButtonNames()
    {
        AddStore("Markt");
        AddStore("Bio-Laden");

        var cut = Render<StoresPage>();

        Assert.Equal(2, cut.FindAll("li.fa-list-row").Count);
        Assert.NotNull(cut.Find("button[aria-label='Markt bearbeiten']"));
        Assert.NotNull(cut.Find("button[aria-label='Markt löschen']"));
        Assert.NotNull(cut.Find("button[aria-label='Reihenfolge von Markt']"));
    }

    [Fact]
    public void Create_AddsTheStore()
    {
        AddStore("Markt");
        var cut = Render<StoresPage>();

        cut.FindAll("button").Last(b => b.TextContent.Contains("Laden anlegen", StringComparison.Ordinal)).Click();
        Dialogs.WaitForAssertion(() => Assert.Contains("Neuer Laden", Dialogs.Markup, StringComparison.Ordinal));
        InputFor(Dialogs, "Name").Change("Bio-Laden");
        ButtonByText(Dialogs, "Speichern").Click();

        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll("li.fa-list-row").Count));
        Assert.Contains(Stores.Stores, s => s.Name == "Bio-Laden");
    }

    [Fact]
    public void FailedLoad_ShowsTheError_NotTheEmptyState()
    {
        Stores.FailWith = FakeAccountClient.Error(0, FoodAssistant.Client.Http.ClientErrorCodes.Network);

        var cut = Render<StoresPage>();

        Assert.NotNull(cut.Find("[role=alert]"));
        Assert.DoesNotContain("Noch keine Läden", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ConflictOnCreate_ThatMeansTheStoreExists_TakesThatStore()
    {
        var dialog = await ShowAsync<FoodAssistant.UI.Stores.StoreDialog>();
        Stores.CreateConflictOnce = true;

        InputFor(Dialogs, "Name").Change("Markt");
        ButtonByText(Dialogs, "Speichern").Click();

        var result = await dialog.Result.WaitAsync(TimeSpan.FromSeconds(10), Xunit.TestContext.Current.CancellationToken);
        Assert.False(result!.Canceled);
        Assert.Single(Stores.Stores);
        Assert.Single(Stores.Calls, c => c == "create-store");
    }

    [Fact]
    public async Task RecoveredStore_WithAnotherName_IsRenamedToWhatIsTyped()
    {
        var dialog = await ShowAsync<FoodAssistant.UI.Stores.StoreDialog>();
        Stores.CreateConflictOnce = true;
        Stores.ConflictedName = "Alt";

        InputFor(Dialogs, "Name").Change("Markt");
        ButtonByText(Dialogs, "Speichern").Click();

        await dialog.Result.WaitAsync(TimeSpan.FromSeconds(10), Xunit.TestContext.Current.CancellationToken);
        Assert.Equal("Markt", Assert.Single(Stores.Stores).Name);
        Assert.Contains(Stores.Calls, c => c.StartsWith("update-store:", StringComparison.Ordinal));
    }

    [Fact]
    public void Dialog_RefusesAnEmptyName()
    {
        var cut = Render<StoresPage>();
        cut.FindAll("button").First(b => b.TextContent.Contains("Laden anlegen", StringComparison.Ordinal)).Click();
        Dialogs.WaitForAssertion(() => Assert.Contains("Neuer Laden", Dialogs.Markup, StringComparison.Ordinal));

        ButtonByText(Dialogs, "Speichern").Click();

        Assert.Contains("Pflichtfeld", Dialogs.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("create-store", Stores.Calls);
    }

    [Fact]
    public void Rename_UsesTheEtag()
    {
        var store = AddStore("Markt", version: 4);
        var cut = Render<StoresPage>();

        cut.Find("button[aria-label='Markt bearbeiten']").Click();
        Dialogs.WaitForAssertion(() => Assert.Contains("Laden bearbeiten", Dialogs.Markup, StringComparison.Ordinal));
        InputFor(Dialogs, "Name").Change("Supermarkt");
        ButtonByText(Dialogs, "Speichern").Click();

        cut.WaitForAssertion(() => Assert.Contains("Supermarkt", cut.Find("li.fa-list-row").TextContent, StringComparison.Ordinal));
        Assert.Equal(5, Stores.Stores.Single(s => s.Id == store.Id).Version);
    }

    [Fact]
    public void Rename_AfterSomeoneElseChangedTheStore_ReloadsTheCurrentState()
    {
        var store = AddStore("Markt", version: 1);
        var cut = Render<StoresPage>();
        cut.Find("button[aria-label='Markt bearbeiten']").Click();
        Dialogs.WaitForAssertion(() => Assert.Contains("Laden bearbeiten", Dialogs.Markup, StringComparison.Ordinal));
        Stores.Stores[0] = store with { Name = "Von anderen", Version = 2 };

        ButtonByText(Dialogs, "Speichern").Click();

        Dialogs.WaitForAssertion(() => Assert.Contains("inzwischen geändert", Dialogs.Markup, StringComparison.Ordinal));
        Assert.Equal("Von anderen", InputFor(Dialogs, "Name").GetAttribute("value"));
    }

    [Fact]
    public void Rename_AfterAnotherCheckOffChangedOnlyTheVersion_SucceedsWithoutWarning()
    {
        var store = AddStore("Markt", version: 1);
        var cut = Render<StoresPage>();
        cut.Find("button[aria-label='Markt bearbeiten']").Click();
        Dialogs.WaitForAssertion(() => Assert.Contains("Laden bearbeiten", Dialogs.Markup, StringComparison.Ordinal));
        InputFor(Dialogs, "Name").Change("Supermarkt");
        // A check-off elsewhere changed the learned fields of the store row, not its name.
        Stores.Stores[0] = store with { Version = 5 };

        ButtonByText(Dialogs, "Speichern").Click();

        cut.WaitForAssertion(() => Assert.Contains("Supermarkt", cut.Find("li.fa-list-row").TextContent, StringComparison.Ordinal));
        Assert.DoesNotContain("inzwischen geändert", Dialogs.Markup, StringComparison.Ordinal);
        Assert.Equal(6, Stores.Stores[0].Version);
    }

    [Fact]
    public void Delete_AsksForConfirmation()
    {
        AddStore("Markt");
        var cut = Render<StoresPage>();

        cut.Find("button[aria-label='Markt löschen']").Click();
        Dialogs.WaitForAssertion(() => Assert.Contains("Laden löschen?", Dialogs.Markup, StringComparison.Ordinal));
        Dialogs.Find(".mud-message-box__yes-button").Click();

        cut.WaitForAssertion(() => Assert.Contains("Noch keine Läden", cut.Markup, StringComparison.Ordinal));
        Assert.Empty(Stores.Stores);
    }

    [Fact]
    public void SelectingAStore_ShowsItsSectionOrder_AndSelectingAgainHidesIt()
    {
        var store = AddStore("Markt");
        AddStore("Bio-Laden");
        var cut = Render<StoresPage>();
        Assert.Empty(cut.FindAll(".fa-order-list"));

        cut.Find("button[aria-label='Reihenfolge von Markt']").Click();

        cut.WaitForAssertion(() => Assert.Equal(11, cut.FindAll(".fa-order-name").Count));
        Assert.Equal("Reihenfolge der Bereiche: Markt", cut.Find("h2").TextContent);
        Assert.Equal("true", cut.Find("button[aria-label='Reihenfolge von Markt']").GetAttribute("aria-expanded"));
        Assert.Equal("fa-store-order", cut.Find("button[aria-label='Reihenfolge von Markt']").GetAttribute("aria-controls"));
        Assert.NotNull(cut.Find("#fa-store-order"));
        Assert.Contains($"get-order:{store.Id}", Stores.Calls);

        cut.Find("button[aria-label='Reihenfolge von Markt']").Click();

        Assert.Empty(cut.FindAll(".fa-order-list"));
    }

    [Fact]
    public async Task LiveStoreChange_ReloadsTheList()
    {
        AddStore("Markt");
        var cut = Render<StoresPage>();

        AddStore("Bio-Laden");
        await cut.InvokeAsync(() => Live.RaiseChangedAsync(new HouseholdChangesMessage(
            FakeAccountClient.TestHouseholdId, [new HouseholdChangeDto(SyncEntityTypes.Store, Guid.CreateVersion7(), 3, false)])));

        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll("li.fa-list-row").Count));
    }

    [Fact]
    public async Task Resync_ReloadsTheList()
    {
        AddStore("Markt");
        var cut = Render<StoresPage>();

        AddStore("Bio-Laden");
        await cut.InvokeAsync(() => Live.RaiseResyncedAsync());

        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll("li.fa-list-row").Count));
    }

    [Fact]
    public void English_UsesEnglishTexts()
    {
        UseCulture("en");
        AddStore("Market");

        var cut = Render<StoresPage>();

        Assert.Equal("Stores", cut.Find("h1").TextContent);
        Assert.NotNull(cut.Find("button[aria-label='Order of Market']"));
        Assert.NotNull(cut.Find("button[aria-label='Edit Market']"));
    }
}

public sealed class StoresPageRestrictedTests() : ShoppingTestContext(role: HouseholdRole.Restricted)
{
    [Fact]
    public void RestrictedMember_SeesStoresAndOrder_ButCannotChangeAnything()
    {
        AddStore("Markt");
        var cut = Render<StoresPage>();

        Assert.Single(cut.FindAll("li.fa-list-row"));
        Assert.Empty(cut.FindAll("button[aria-label$='bearbeiten'], button[aria-label$='löschen']"));
        Assert.DoesNotContain(cut.FindAll("button"), b => b.TextContent.Contains("Laden anlegen", StringComparison.Ordinal));

        cut.Find("button[aria-label='Reihenfolge von Markt']").Click();

        cut.WaitForAssertion(() => Assert.Equal(11, cut.FindAll(".fa-order-name").Count));
        Assert.Empty(cut.FindAll("button[aria-label$='nach oben']"));
    }
}
