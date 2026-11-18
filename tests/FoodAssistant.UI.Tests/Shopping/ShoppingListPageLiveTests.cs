using Bunit;
using FoodAssistant.Contracts.Sync;
using FoodAssistant.UI.Common;
using FoodAssistant.UI.Shopping;
using FoodAssistant.UI.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace FoodAssistant.UI.Tests.Shopping;

/// <summary>Live updates, resync and the announcements of check-offs on the list page.</summary>
public sealed class ShoppingListPageLiveTests : ShoppingTestContext
{
    private IRenderedComponent<ShoppingListPage> RenderList(Guid listId) =>
        Render<ShoppingListPage>(p => p.Add(c => c.ListId, listId));

    private Task RaiseAsync(IRenderedComponent<ShoppingListPage> cut, string entityType) =>
        cut.InvokeAsync(() => Live.RaiseChangedAsync(new HouseholdChangesMessage(
            FakeAccountClient.TestHouseholdId, [new HouseholdChangeDto(entityType, Guid.CreateVersion7(), 9, false)])));

    private ILiveAnnouncer Announcer => Services.GetRequiredService<ILiveAnnouncer>();

    private int OrderCalls => Stores.Calls.Count(c => c.StartsWith("get-order:", StringComparison.Ordinal));

    [Fact]
    public async Task ForeignCheckOff_IsAnnouncedPolitely()
    {
        var list = AddList();
        var milk = AddItem(list.Id, "Milch", "dairy_chilled");
        AddItem(list.Id, "Äpfel", "produce");
        var cut = RenderList(list.Id);

        Shopping.Items[Shopping.Items.FindIndex(i => i.Id == milk.Id)] = milk with { Checked = true, Version = milk.Version + 1 };
        await RaiseAsync(cut, SyncEntityTypes.ShoppingItem);

        cut.WaitForAssertion(() => Assert.Equal("Milch wurde abgehakt.", Announcer.Message.Trim()));
        Assert.Contains("Im Wagen (1)", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OwnCheckOff_WhoseLiveMessageArrivesBeforeTheResponse_IsNotAnnouncedAsForeign()
    {
        var list = AddList();
        AddItem(list.Id, "Milch", "dairy_chilled");
        var cut = RenderList(list.Id);
        // The server publishes the change before it writes the response: the item is checked when the page reloads.
        Shopping.HoldResponse = new TaskCompletionSource();

        cut.Find("button[aria-label='Milch abhaken']").Click();
        await RaiseAsync(cut, SyncEntityTypes.ShoppingItem);

        Assert.Equal(string.Empty, Announcer.Message);
        Assert.Contains("Im Wagen (1)", cut.Markup, StringComparison.Ordinal);

        Shopping.HoldResponse.SetResult();
        cut.WaitForAssertion(() => Assert.Equal("Milch im Wagen", Announcer.Message.Trim()));
    }

    [Fact]
    public void OwnCheck_AndUncheck_AreToldAsMovesBetweenListAndCart()
    {
        var list = AddList();
        AddItem(list.Id, "Milch");
        var cut = RenderList(list.Id);

        cut.Find("button[aria-label='Milch abhaken']").Click();
        cut.WaitForAssertion(() => Assert.Equal("Milch im Wagen", Announcer.Message.Trim()));
        cut.Find("button.fa-cart-toggle").Click();
        cut.Find("button[aria-label='Milch zurücknehmen']").Click();

        cut.WaitForAssertion(() => Assert.Equal("Milch wieder auf der Liste", Announcer.Message.Trim()));
    }

    [Fact]
    public async Task ItemChange_ReloadsTheItems_ButNotTheSectionOrder()
    {
        var list = AddList();
        var cut = RenderList(list.Id);
        var before = OrderCalls;

        AddItem(list.Id, "Brot", "bakery");
        await RaiseAsync(cut, SyncEntityTypes.ShoppingItem);

        cut.WaitForAssertion(() => Assert.Equal(["Brot & Backwaren"], cut.FindAll("h2").Select(h => h.TextContent.Trim())));
        Assert.Equal(before, OrderCalls);
    }

    [Theory]
    [InlineData(SyncEntityTypes.ShoppingList)]
    [InlineData(SyncEntityTypes.Store)]
    public async Task ListOrStoreChange_ReloadsTheSectionOrder(string entityType)
    {
        var list = AddList();
        var cut = RenderList(list.Id);
        var before = OrderCalls;

        await RaiseAsync(cut, entityType);

        cut.WaitForAssertion(() => Assert.Equal(before + 1, OrderCalls));
    }

    [Fact]
    public async Task Resync_ReloadsItemsAndOrder_WithoutAnnouncing()
    {
        var list = AddList();
        var milk = AddItem(list.Id, "Milch", "dairy_chilled");
        var cut = RenderList(list.Id);
        var before = OrderCalls;

        Shopping.Items[0] = milk with { Checked = true, Version = milk.Version + 1 };
        AddItem(list.Id, "Brot", "bakery");
        await cut.InvokeAsync(() => Live.RaiseResyncedAsync());

        cut.WaitForAssertion(() => Assert.Contains("Im Wagen (1)", cut.Markup, StringComparison.Ordinal));
        Assert.Contains(cut.FindAll("h2"), h => h.TextContent.Trim() == "Brot & Backwaren");
        Assert.Equal(before + 1, OrderCalls);
        Assert.Equal(string.Empty, Announcer.Message);
    }

    [Fact]
    public async Task Disposing_StopsListeningToTheLiveUpdates()
    {
        var list = AddList();
        var cut = RenderList(list.Id);
        var calls = Shopping.Calls.Count;

        cut.Instance.Dispose();
        await Live.RaiseResyncedAsync();

        Assert.Equal(calls, Shopping.Calls.Count);
    }
}
