using Bunit;
using FoodAssistant.Contracts.Households;
using FoodAssistant.Contracts.Inventory;
using FoodAssistant.Contracts.Sync;
using FoodAssistant.UI.Common;
using FoodAssistant.UI.Inventory;
using FoodAssistant.UI.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace FoodAssistant.UI.Tests.Inventory;

public sealed class InventoryPageTests : InventoryTestContext
{
    [Fact]
    public void NoStock_ShowsTheEmptyState()
    {
        var cut = Render<InventoryPage>();

        Assert.Equal("Vorrat", cut.Find("h1").TextContent);
        Assert.Contains("Dein Vorrat ist noch leer", cut.Find(".fa-empty-heading").TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void Products_ShowCountTotalLocationAndStatusOfTheEarliestBatch()
    {
        var milk = Summary("Milch");
        Stock(milk, Batch(milk, 1, ExpiryStatusDto.Soon, 0.5m), Batch(milk, 9, ExpiryStatusDto.Ok));

        var cut = Render<InventoryPage>();

        var row = cut.Find("li.fa-list-row");
        Assert.Contains("2 Chargen · 1,5 l · Kühlschrank", row.TextContent, StringComparison.Ordinal);
        Assert.Contains("morgen", row.QuerySelector(".fa-status-chip")!.TextContent, StringComparison.Ordinal);
        Assert.Contains("Läuft bald ab", row.QuerySelector(".fa-status-chip")!.TextContent, StringComparison.Ordinal);
        Assert.Equal("Chargen von Milch", row.QuerySelector("button")!.GetAttribute("aria-label"));
    }

    [Fact]
    public void Filters_AskTheServer_AndMarkTheActiveFilter()
    {
        var cut = Render<InventoryPage>();

        cut.FindAll("button").First(b => b.TextContent.Trim() == "Abgelaufen").Click();

        Assert.Contains("list:expired::", Inventory.Calls);
        var active = cut.FindAll("button[aria-pressed='true']");
        Assert.Equal("Abgelaufen", Assert.Single(active).TextContent.Trim());
    }

    [Fact]
    public void Restricted_SeesNoAddButton()
    {
        Accounts.Me = FakeAccountClient.MeWith(HouseholdRole.Restricted);
        ReloadSession();

        var cut = Render<InventoryPage>();

        Assert.DoesNotContain(cut.FindAll("button"), b => b.TextContent.Contains("Einbuchen", StringComparison.Ordinal));
    }

    [Fact]
    public async Task LiveChangeOfAnotherMember_ReloadsAndIsAnnounced()
    {
        var cut = Render<InventoryPage>();
        var before = Inventory.Calls.Count(c => c.StartsWith("list:", StringComparison.Ordinal));
        var milk = Summary("Milch");
        Stock(milk, Batch(milk, 3, ExpiryStatusDto.Ok));

        await cut.InvokeAsync(() => Live.RaiseChangedAsync(new HouseholdChangesMessage(
            FakeAccountClient.TestHouseholdId, [new HouseholdChangeDto(SyncEntityTypes.Batch, Guid.CreateVersion7(), 5, false)])));

        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("li.fa-list-row")));
        Assert.True(Inventory.Calls.Count(c => c.StartsWith("list:", StringComparison.Ordinal)) > before);
        Assert.Equal("Der Vorrat wurde von jemand anderem geändert.", Services.GetRequiredService<ILiveAnnouncer>().Message);
    }

    [Fact]
    public void Add_WithoutProduct_SendsNothing()
    {
        var cut = Render<InventoryPage>();

        cut.FindAll("button").First(b => b.TextContent.Contains("Einbuchen", StringComparison.Ordinal)).Click();
        Dialogs.WaitForAssertion(() => Assert.Contains("Lagerort", Dialogs.Markup, StringComparison.Ordinal));
        ButtonByText(Dialogs, "Speichern").Click();

        Assert.DoesNotContain("add", Inventory.Calls);
        Assert.Contains("Bitte ausfüllen.", Dialogs.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Add_PickedProduct_SavesReloadsAndOffersUndo()
    {
        Catalog.Products.Add(Product("Milch"));
        var activity = Guid.CreateVersion7();
        Inventory.NextAction = new InventoryActionResponse([], [], activity);
        var cut = Render<InventoryPage>();
        cut.FindAll("button").First(b => b.TextContent.Contains("Einbuchen", StringComparison.Ordinal)).Click();
        Dialogs.WaitForAssertion(() => Assert.Contains("Lagerort", Dialogs.Markup, StringComparison.Ordinal));

        Dialogs.Find("input").Input("Mil");
        Popovers.WaitForAssertion(() => Assert.Contains("Milch", Popovers.Markup, StringComparison.Ordinal));
        Popovers.FindAll(".mud-list-item").First(i => i.TextContent.Contains("Milch", StringComparison.Ordinal)).Click();
        ButtonByText(Dialogs, "Speichern").Click();

        Assert.Contains("add", Inventory.Calls);
        Snackbars.WaitForAssertion(() => Assert.Contains("Eingebucht", Snackbars.Markup, StringComparison.Ordinal));
        Snackbars.Find(".mud-snackbar-action-button, .mud-snackbar-action button").Click();
        Snackbars.WaitForAssertion(() => Assert.Contains($"undo:{activity}", Inventory.Calls));
    }
}
