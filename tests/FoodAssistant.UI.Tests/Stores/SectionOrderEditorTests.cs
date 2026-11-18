using Bunit;
using FoodAssistant.Contracts.Households;
using FoodAssistant.Contracts.Stores;
using FoodAssistant.Contracts.Sync;
using FoodAssistant.UI.Catalog;
using FoodAssistant.UI.Common;
using FoodAssistant.UI.Tests.Shopping;
using FoodAssistant.UI.Tests.Support;
using FoodAssistant.UI.Stores;
using Microsoft.Extensions.DependencyInjection;

namespace FoodAssistant.UI.Tests.Stores;

public sealed class SectionOrderEditorTests : ShoppingTestContext
{
    private IRenderedComponent<SectionOrderEditor> RenderEditor(Guid storeId, string name = "Markt") =>
        Render<SectionOrderEditor>(p => p.Add(c => c.StoreId, storeId).Add(c => c.StoreName, name));

    private static IReadOnlyList<string> Names(IRenderedComponent<SectionOrderEditor> cut) =>
        [.. cut.FindAll(".fa-order-name").Select(n => n.TextContent.Trim())];

    [Fact]
    public void LearnedOrder_IsShownAsLearned_WithAllElevenSections()
    {
        var store = AddStore("Markt");
        Stores.Learned[store.Id] = ["dairy_chilled", .. SectionKeys.All.Except(["dairy_chilled"])];

        var cut = RenderEditor(store.Id);

        Assert.Equal("Reihenfolge der Bereiche: Markt", cut.Find("h2").TextContent);
        Assert.Contains("Gelernt", cut.Find(".fa-order-kind").TextContent, StringComparison.Ordinal);
        Assert.Equal(11, Names(cut).Count);
        Assert.Equal("Kühlregal", Names(cut)[0]);
        Assert.Equal("Obst & Gemüse", Names(cut)[1]);
    }

    [Fact]
    public void ManualOrder_IsShownAsOwnOrder()
    {
        var store = AddStore("Markt");
        Stores.Manual[store.Id] = [.. SectionKeys.All.Reverse()];

        var cut = RenderEditor(store.Id);

        Assert.Contains("Eigene Reihenfolge", cut.Find(".fa-order-kind").TextContent, StringComparison.Ordinal);
        Assert.Equal("Sonstiges", Names(cut)[0]);
    }

    [Fact]
    public void EveryButton_NamesTheSectionAndTheDirection()
    {
        var store = AddStore("Markt");

        var cut = RenderEditor(store.Id);

        Assert.NotNull(cut.Find("button[aria-label='Obst & Gemüse nach oben']"));
        Assert.NotNull(cut.Find("button[aria-label='Obst & Gemüse nach unten']"));
        Assert.Equal(22, cut.FindAll("button[aria-label$='nach oben'], button[aria-label$='nach unten']").Count);
    }

    [Fact]
    public void TheFirstCannotMoveUp_TheLastCannotMoveDown()
    {
        var store = AddStore("Markt");

        var cut = RenderEditor(store.Id);

        Assert.True(cut.Find("button[aria-label='Obst & Gemüse nach oben']").HasAttribute("aria-disabled"));
        Assert.True(cut.Find("button[aria-label='Sonstiges nach unten']").HasAttribute("aria-disabled"));
        Assert.False(cut.Find("button[aria-label='Sonstiges nach oben']").HasAttribute("aria-disabled"));
    }

    [Fact]
    public void ButtonsThatCannotAct_StayFocusable_AndDoNothing()
    {
        var store = AddStore("Markt");
        var cut = RenderEditor(store.Id);

        var first = cut.Find("button[aria-label='Obst & Gemüse nach oben']");
        Assert.False(first.HasAttribute("disabled"));
        first.Click();
        FindByText(cut, "Reihenfolge speichern").Click();
        FindByText(cut, "Zurück zur gelernten").Click();

        Assert.Equal("Obst & Gemüse", Names(cut)[0]);
        Assert.DoesNotContain(Stores.Calls, c => c.StartsWith("set-order", StringComparison.Ordinal) || c.StartsWith("reset-order", StringComparison.Ordinal));
    }

    [Fact]
    public void MovingDown_SwapsTheSections_AndAnnouncesThePosition()
    {
        var store = AddStore("Markt");
        var cut = RenderEditor(store.Id);

        cut.Find("button[aria-label='Obst & Gemüse nach unten']").Click();

        Assert.Equal(["Brot & Backwaren", "Obst & Gemüse"], Names(cut).Take(2));
        Assert.Equal("Obst & Gemüse: Position 2 von 11", Services.GetRequiredService<ILiveAnnouncer>().Message.Trim());
        Assert.DoesNotContain(Stores.Calls, c => c.StartsWith("set-order", StringComparison.Ordinal));
    }

    [Fact]
    public void MovingUp_SwapsWithThePredecessor()
    {
        var store = AddStore("Markt");
        var cut = RenderEditor(store.Id);

        cut.Find("button[aria-label='Getränke nach oben']").Click();

        var names = Names(cut);
        Assert.Equal("Getränke", names[7]);
    }

    [Fact]
    public void Save_IsOnlyPossibleAfterAChange_AndSendsTheWholeShownOrder()
    {
        var store = AddStore("Markt");
        var cut = RenderEditor(store.Id);
        Assert.True(FindByText(cut, "Reihenfolge speichern").HasAttribute("aria-disabled"));

        cut.Find("button[aria-label='Obst & Gemüse nach unten']").Click();
        FindByText(cut, "Reihenfolge speichern").Click();

        cut.WaitForAssertion(() => Assert.Contains("Eigene Reihenfolge", cut.Find(".fa-order-kind").TextContent, StringComparison.Ordinal));
        var sent = Stores.Manual[store.Id];
        Assert.Equal(11, sent.Count);
        Assert.Equal(["bakery", "produce"], sent.Take(2));
        Assert.True(FindByText(cut, "Reihenfolge speichern").HasAttribute("aria-disabled"));
    }

    [Fact]
    public void Reset_IsOnlyOfferedForAManualOrder_AndReturnsToTheLearnedOne()
    {
        var store = AddStore("Markt");
        Stores.Learned[store.Id] = ["dairy_chilled", .. SectionKeys.All.Except(["dairy_chilled"])];
        Stores.Manual[store.Id] = SectionKeys.All;
        var cut = RenderEditor(store.Id);
        Assert.Equal("Obst & Gemüse", Names(cut)[0]);

        FindByText(cut, "Zurück zur gelernten").Click();

        cut.WaitForAssertion(() => Assert.Contains("Gelernt", cut.Find(".fa-order-kind").TextContent, StringComparison.Ordinal));
        Assert.Equal("Kühlregal", Names(cut)[0]);
        Assert.Empty(Stores.Manual);
        Assert.True(FindByText(cut, "Zurück zur gelernten").HasAttribute("aria-disabled"));
    }

    [Fact]
    public void FailingSave_ShowsTheError_AndKeepsTheShownOrder()
    {
        var store = AddStore("Markt");
        var cut = RenderEditor(store.Id);
        cut.Find("button[aria-label='Obst & Gemüse nach unten']").Click();
        Stores.FailWith = FakeAccountClient.Error(404, StoreErrorCodes.StoreNotFound);

        FindByText(cut, "Reihenfolge speichern").Click();

        cut.WaitForAssertion(() => Assert.Contains("Laden gibt es nicht mehr", cut.Find("[role=alert]").TextContent, StringComparison.Ordinal));
        Assert.Equal("Brot & Backwaren", Names(cut)[0]);
    }

    private Task RaiseStoreChangeAsync(IRenderedComponent<SectionOrderEditor> cut) =>
        cut.InvokeAsync(() => Live.RaiseChangedAsync(new HouseholdChangesMessage(
            FakeAccountClient.TestHouseholdId, [new HouseholdChangeDto(SyncEntityTypes.Store, Guid.CreateVersion7(), 4, false)])));

    [Fact]
    public async Task LiveStoreChange_ShowsTheNewlyLearnedOrder()
    {
        var store = AddStore("Markt");
        var cut = RenderEditor(store.Id);

        Stores.Learned[store.Id] = ["dairy_chilled", .. SectionKeys.All.Except(["dairy_chilled"])];
        await RaiseStoreChangeAsync(cut);

        cut.WaitForAssertion(() => Assert.Equal("Kühlregal", Names(cut)[0]));
    }

    [Fact]
    public async Task LiveChange_NeverOverwritesUnsavedMoves()
    {
        var store = AddStore("Markt");
        var cut = RenderEditor(store.Id);
        cut.Find("button[aria-label='Obst & Gemüse nach unten']").Click();

        Stores.Learned[store.Id] = ["dairy_chilled", .. SectionKeys.All.Except(["dairy_chilled"])];
        await RaiseStoreChangeAsync(cut);
        await cut.InvokeAsync(() => Live.RaiseResyncedAsync());

        Assert.Equal("Brot & Backwaren", Names(cut)[0]);
    }

    [Fact]
    public async Task Resync_ReloadsTheOrder()
    {
        var store = AddStore("Markt");
        var cut = RenderEditor(store.Id);

        Stores.Learned[store.Id] = ["dairy_chilled", .. SectionKeys.All.Except(["dairy_chilled"])];
        await cut.InvokeAsync(() => Live.RaiseResyncedAsync());

        cut.WaitForAssertion(() => Assert.Equal("Kühlregal", Names(cut)[0]));
    }

    [Fact]
    public void English_UsesEnglishTexts()
    {
        UseCulture("en");
        var store = AddStore("Market");

        var cut = RenderEditor(store.Id, "Market");

        Assert.Equal("Section order: Market", cut.Find("h2").TextContent);
        Assert.Contains("Learned", cut.Find(".fa-order-kind").TextContent, StringComparison.Ordinal);
        Assert.NotNull(cut.Find("button[aria-label='Move Fruit & vegetables down']"));
        Assert.NotNull(FindByText(cut, "Back to learned order"));
    }

    private static AngleSharp.Dom.IElement FindByText(IRenderedComponent<SectionOrderEditor> cut, string text) =>
        cut.FindAll("button").First(b => b.TextContent.Trim() == text);
}

public sealed class SectionOrderEditorRestrictedTests() : ShoppingTestContext(role: HouseholdRole.Restricted)
{
    [Fact]
    public void RestrictedMember_SeesTheOrder_ButNoButtons()
    {
        var store = AddStore("Markt");

        var cut = Render<SectionOrderEditor>(p => p.Add(c => c.StoreId, store.Id).Add(c => c.StoreName, "Markt"));

        Assert.Equal(11, cut.FindAll(".fa-order-name").Count);
        Assert.Empty(cut.FindAll("button"));
    }
}
