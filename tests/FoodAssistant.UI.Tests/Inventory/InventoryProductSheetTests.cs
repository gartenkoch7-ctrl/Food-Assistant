using Bunit;
using FoodAssistant.Contracts.Households;
using FoodAssistant.Contracts.Inventory;
using FoodAssistant.UI.Inventory;
using FoodAssistant.UI.Tests.Support;

namespace FoodAssistant.UI.Tests.Inventory;

public sealed class InventoryProductSheetTests : InventoryTestContext
{
    [Fact]
    public async Task Batches_ShowAmountLocationStateAndUseByWithHint()
    {
        var milk = Summary("Milch");
        Stock(milk, Batch(milk, -1, ExpiryStatusDto.Expired, kind: ExpiryKindDto.UseBy, state: BatchStateDto.Opened));

        await ShowAsync<InventoryProductSheet>(p => { p.Add(d => d.Product, milk); p.Add(d => d.Locations, Locations); });

        Dialogs.WaitForAssertion(() => Assert.Contains("1 l · Kühlschrank · geöffnet", Dialogs.Markup, StringComparison.Ordinal));
        var chip = Dialogs.Find(".fa-status-chip").TextContent;
        Assert.Contains("Verbrauchsdatum: seit gestern abgelaufen", chip, StringComparison.Ordinal);
        Assert.Contains("Abgelaufen", chip, StringComparison.Ordinal);
        Assert.Contains("nicht mehr essen", Dialogs.Find("[role='note']").TextContent, StringComparison.Ordinal);
        Assert.Equal("Aktionen für 1 l · Kühlschrank · geöffnet", Dialogs.Find("[role='group']").GetAttribute("aria-label"));
        Assert.DoesNotContain(Dialogs.FindAll("button"), b => b.TextContent.Trim() == "Geöffnet");
    }

    [Fact]
    public async Task UsedUp_SendsTheVersion_ThenUndoCallsTheServer()
    {
        var milk = Summary("Milch");
        var batch = Batch(milk, 3, ExpiryStatusDto.Ok);
        Stock(milk, batch);
        var activity = Guid.CreateVersion7();
        Inventory.NextAction = new InventoryActionResponse([], [batch.Id], activity);
        await ShowAsync<InventoryProductSheet>(p => { p.Add(d => d.Product, milk); p.Add(d => d.Locations, Locations); });
        Dialogs.WaitForAssertion(() => Assert.Contains("Aufgebraucht", Dialogs.Markup, StringComparison.Ordinal));

        ButtonByText(Dialogs, "Aufgebraucht").Click();

        Assert.Contains($"consume:{batch.Id}@\"7\"", Inventory.Calls);
        Assert.Equal(ConsumptionKindDto.UsedUp, Assert.IsType<ConsumeBatchRequest>(Assert.Single(Inventory.Requests)).Kind);
        // Undo sits inside the sheet (the dialog's focus trap keeps the keyboard away from snackbars) and gets the focus.
        Dialogs.WaitForAssertion(() => Assert.Contains("Als aufgebraucht markiert", Dialogs.Find("[role='status']").TextContent, StringComparison.Ordinal));
        Assert.Contains("fa-sheet-undo", Browser.FocusedElements);
        Dialogs.Find("#fa-sheet-undo").Click();
        Dialogs.WaitForAssertion(() => Assert.Contains($"undo:{activity}", Inventory.Calls));
        Assert.Empty(Dialogs.FindAll("#fa-sheet-undo"));
        Assert.Equal("Rückgängig gemacht", Dialogs.Find("#fa-sheet-status").TextContent.Trim());
        Assert.Equal("fa-sheet-status", Browser.FocusedElements[^1]);
    }

    [Fact]
    public async Task StaleVersion_ReloadsTheBatch_AndSaysItChanged()
    {
        var milk = Summary("Milch");
        Stock(milk, Batch(milk, 3, ExpiryStatusDto.Ok));
        Inventory.FailWith = new Client.Http.ApiError(412, "common.precondition_failed", null, new Dictionary<string, string[]>(), "\"8\"");
        await ShowAsync<InventoryProductSheet>(p => { p.Add(d => d.Product, milk); p.Add(d => d.Locations, Locations); });
        Dialogs.WaitForAssertion(() => Assert.Contains("Aufgebraucht", Dialogs.Markup, StringComparison.Ordinal));
        var loads = Inventory.Calls.Count(c => c.StartsWith("product:", StringComparison.Ordinal));

        ButtonByText(Dialogs, "Aufgebraucht").Click();

        Dialogs.WaitForAssertion(() => Assert.Equal(loads + 1, Inventory.Calls.Count(c => c.StartsWith("product:", StringComparison.Ordinal))));
        Assert.NotEmpty(Dialogs.FindAll("[role='alert']"));
    }

    [Fact]
    public async Task Restricted_SeesNoActions()
    {
        Accounts.Me = FakeAccountClient.MeWith(HouseholdRole.Restricted);
        ReloadSession();
        var milk = Summary("Milch");
        Stock(milk, Batch(milk, 3, ExpiryStatusDto.Ok));

        await ShowAsync<InventoryProductSheet>(p => { p.Add(d => d.Product, milk); p.Add(d => d.Locations, Locations); });

        Dialogs.WaitForAssertion(() => Assert.Contains("Kühlschrank", Dialogs.Markup, StringComparison.Ordinal));
        Assert.Empty(Dialogs.FindAll("[role='group']"));
    }

    [Fact]
    public async Task FailedAction_ShowsTheTranslatedError()
    {
        var milk = Summary("Milch");
        Stock(milk, Batch(milk, 3, ExpiryStatusDto.Ok));
        Inventory.FailWith = FakeAccountClient.Error(422, "inventory.invalid_state");
        await ShowAsync<InventoryProductSheet>(p => { p.Add(d => d.Product, milk); p.Add(d => d.Locations, Locations); });
        Dialogs.WaitForAssertion(() => Assert.Contains("Geöffnet", Dialogs.Markup, StringComparison.Ordinal));

        ButtonByText(Dialogs, "Geöffnet").Click();

        Dialogs.WaitForAssertion(() => Assert.Contains("Das geht in diesem Zustand der Charge nicht.", Dialogs.Find("[role='alert']").TextContent, StringComparison.Ordinal));
    }
}
