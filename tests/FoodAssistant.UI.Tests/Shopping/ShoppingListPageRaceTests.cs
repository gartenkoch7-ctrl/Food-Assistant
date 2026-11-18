using Bunit;
using FoodAssistant.Contracts.Sync;
using FoodAssistant.UI.Catalog;
using FoodAssistant.UI.Common;
using FoodAssistant.UI.Shopping;
using FoodAssistant.UI.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace FoodAssistant.UI.Tests.Shopping;

/// <summary>Stale answers, reused components and states without a list.</summary>
public sealed class ShoppingListPageRaceTests : ShoppingTestContext
{
    private IRenderedComponent<ShoppingListPage> RenderList(Guid listId) =>
        Render<ShoppingListPage>(p => p.Add(c => c.ListId, listId));

    [Fact]
    public async Task OlderResponse_DoesNotOverwriteANewerStateFromALiveReload()
    {
        var list = AddList();
        var milk = AddItem(list.Id, "Milch", "dairy_chilled");
        var cut = RenderList(list.Id);
        Shopping.HoldResponse = new TaskCompletionSource();

        // The server applies the check-off (version 4) and holds the response ...
        cut.Find("button[aria-label='Milch abhaken']").Click();
        // ... and someone changes the note again (version 6) before the response arrives.
        Shopping.Items[0] = Shopping.Items[0] with { Note = "neuer", Version = milk.Version + 3 };
        await cut.InvokeAsync(() => Live.RaiseChangedAsync(new HouseholdChangesMessage(
            FakeAccountClient.TestHouseholdId, [new HouseholdChangeDto(SyncEntityTypes.ShoppingItem, milk.Id, 9, false)])));
        cut.Find("button.fa-cart-toggle").Click();
        Assert.Contains("neuer", cut.Find(".fa-list-row-secondary").TextContent, StringComparison.Ordinal);

        Shopping.HoldResponse.SetResult();

        cut.WaitForAssertion(() => Assert.Equal("Milch im Wagen", Services.GetRequiredService<ILiveAnnouncer>().Message.Trim()));
        Assert.Contains("neuer", cut.Find(".fa-list-row-secondary").TextContent, StringComparison.Ordinal);
    }

    private Task RaiseAsync(IRenderedComponent<ShoppingListPage> cut, string entityType) =>
        cut.InvokeAsync(() => Live.RaiseChangedAsync(new HouseholdChangesMessage(
            FakeAccountClient.TestHouseholdId, [new HouseholdChangeDto(entityType, Guid.CreateVersion7(), 9, false)])));

    [Fact]
    public async Task SupersededReloadThatWasToFetchTheOrder_LeavesItPending_ForTheNextReload()
    {
        var store = AddStore("Markt");
        Stores.Learned[store.Id] = ["dairy_chilled", .. SectionKeys.All.Except(["dairy_chilled"])];
        var list = AddList(storeId: store.Id);
        AddItem(list.Id, "Brot", "bakery");
        AddItem(list.Id, "Milch", "dairy_chilled");
        var cut = RenderList(list.Id);
        Assert.Equal(["Kühlregal", "Brot & Backwaren"], cut.FindAll("h2").Select(h => h.TextContent.Trim()));

        // The store's order changed; the reload that was to fetch it hangs and a plain item reload overtakes it.
        Stores.Learned[store.Id] = ["bakery", .. SectionKeys.All.Except(["bakery"])];
        var hold = new TaskCompletionSource();
        Shopping.HoldListItems = hold;
        var slow = RaiseAsync(cut, SyncEntityTypes.ShoppingList);
        await RaiseAsync(cut, SyncEntityTypes.ShoppingItem);

        Assert.Equal(["Brot & Backwaren", "Kühlregal"], cut.FindAll("h2").Select(h => h.TextContent.Trim()));
        hold.SetResult();
        await slow;
    }

    [Fact]
    public async Task SupersededReloadThatWasToAnnounce_LeavesTheAnnouncementPending()
    {
        var list = AddList();
        var milk = AddItem(list.Id, "Milch", "dairy_chilled");
        AddItem(list.Id, "Äpfel", "produce");
        var cut = RenderList(list.Id);
        Shopping.Items[0] = milk with { Checked = true, Version = milk.Version + 1 };

        // Someone checked the milk: the reload that was to announce it hangs, and a resync (which does not announce) overtakes it.
        var hold = new TaskCompletionSource();
        Shopping.HoldListItems = hold;
        var slow = RaiseAsync(cut, SyncEntityTypes.ShoppingItem);
        await cut.InvokeAsync(() => Live.RaiseResyncedAsync());

        Assert.Equal("Milch wurde abgehakt.", Services.GetRequiredService<ILiveAnnouncer>().Message.Trim());
        hold.SetResult();
        await slow;
    }

    [Fact]
    public void ChangingTheListInTheRoute_ShowsTheOtherList_AndResetsCartAndError()
    {
        var first = AddList();
        var second = AddList("Wochenende", isDefault: false);
        AddItem(first.Id, "Milch", isChecked: true);
        AddItem(second.Id, "Brot", "bakery");
        var cut = RenderList(first.Id);
        cut.Find("button.fa-cart-toggle").Click();
        Assert.Equal("true", cut.Find("button.fa-cart-toggle").GetAttribute("aria-expanded"));

        cut.Render(p => p.Add(c => c.ListId, second.Id));

        cut.WaitForAssertion(() => Assert.Equal("Wochenende", cut.Find("h1").TextContent));
        Assert.Equal(["Brot"], cut.FindAll(".fa-list-row-name").Select(n => n.TextContent.Trim()));
        Assert.Empty(cut.FindAll("button.fa-cart-toggle"));
        Assert.Empty(cut.FindAll("[role=alert]"));
    }

    [Fact]
    public void FailedLoadOfTheNextList_ShowsNothingOfThePreviousOne()
    {
        var first = AddList("Wochenende", isDefault: false);
        var second = AddList();
        var cut = RenderList(first.Id);
        Assert.Equal("Wochenende", cut.Find("h1").TextContent);
        Shopping.FailWith = FakeAccountClient.Error(0, FoodAssistant.Client.Http.ClientErrorCodes.Network);

        cut.Render(p => p.Add(c => c.ListId, second.Id));

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[role=alert]")));
        Assert.DoesNotContain("Wochenende", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain(cut.FindAll("button"), b => b.TextContent.Contains("bearbeiten", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LateAnswerOfARowOfThePreviousList_ShowsNoErrorAndAnnouncesNothing(bool refused)
    {
        var previous = AddList("Wochenende", isDefault: false);
        var current = AddList();
        AddItem(previous.Id, "Milch", isChecked: !refused);
        var cut = RenderList(previous.Id);
        var hold = new TaskCompletionSource();
        Shopping.HoldChange = hold;
        if (!refused)
        {
            // Taking back an item that was checked meanwhile: "checked wins" keeps the server's state (not applied).
            Shopping.ChangeOutcome = FoodAssistant.Contracts.Shopping.ShoppingConflictOutcomeDto.CheckedWins;
            cut.Find("button.fa-cart-toggle").Click();
        }

        cut.Find($"button[aria-label='Milch {(refused ? "abhaken" : "zurücknehmen")}']").Click();
        cut.Render(p => p.Add(c => c.ListId, current.Id));
        cut.WaitForAssertion(() => Assert.NotEqual("Wochenende", cut.Find("h1").TextContent));
        if (refused)
        {
            Shopping.FailChangeWith = FakeAccountClient.Error(409, "shopping.unknown_version");
        }

        hold.SetResult();
        cut.WaitForAssertion(() => Assert.Contains(Shopping.Calls, c => c.StartsWith("change-item", StringComparison.Ordinal)));
        await Task.Delay(100, Xunit.TestContext.Current.CancellationToken);

        Assert.Empty(cut.FindAll("[role=alert]"));
        Assert.Equal(string.Empty, Services.GetRequiredService<ILiveAnnouncer>().Message);
    }

    [Fact]
    public void FailedLoad_ShowsOnlyTheError_NeitherNotFoundNorEmpty()
    {
        var list = AddList();
        Shopping.FailWith = FakeAccountClient.Error(0, FoodAssistant.Client.Http.ClientErrorCodes.Network);

        var cut = RenderList(list.Id);

        Assert.NotNull(cut.Find("[role=alert]"));
        Assert.DoesNotContain("Liste nicht gefunden", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("leer", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void TakingBackAnItemThatWasCheckedMeanwhile_IsAnnouncedAsNotApplied()
    {
        var list = AddList();
        AddItem(list.Id, "Milch", isChecked: true);
        var cut = RenderList(list.Id);
        Shopping.ChangeOutcome = FoodAssistant.Contracts.Shopping.ShoppingConflictOutcomeDto.CheckedWins;
        cut.Find("button.fa-cart-toggle").Click();

        cut.Find("button[aria-label='Milch zurücknehmen']").Click();

        cut.WaitForAssertion(() => Assert.Contains("nicht übernommen", Services.GetRequiredService<ILiveAnnouncer>().Message, StringComparison.Ordinal));
    }

    [Fact]
    public void UnknownList_StillHasAHeading()
    {
        AddList();

        var cut = RenderList(Guid.CreateVersion7());

        Assert.Equal("Einkauf", cut.Find("h1").TextContent);
    }
}
