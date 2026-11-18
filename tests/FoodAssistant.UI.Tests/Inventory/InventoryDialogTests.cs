using Bunit;
using FoodAssistant.Contracts.Common;
using FoodAssistant.Contracts.Inventory;
using FoodAssistant.UI.Inventory;

namespace FoodAssistant.UI.Tests.Inventory;

/// <summary>Add, consume, move, correct and leftover dialogs.</summary>
public sealed class InventoryDialogTests : InventoryTestContext
{
    [Fact]
    public async Task Add_InTheFreezer_OffersSelfFrozen_AndSendsIt()
    {
        var milk = Summary("Milch");
        await ShowAsync<AddBatchDialog>(p => { p.Add(d => d.Product, milk); p.Add(d => d.Locations, new[] { Freezer });
            p.Add(d => d.Quantity, new QuantityDto(2m, "l")); });

        Dialogs.WaitForAssertion(() => Assert.Contains("Selbst eingefroren", Dialogs.Markup, StringComparison.Ordinal));
        Assert.Contains("selbst eingefroren", Dialogs.Markup, StringComparison.Ordinal);
        Dialogs.Find("input[type='checkbox']").Change(true);
        ButtonByText(Dialogs, "Speichern").Click();

        var request = Assert.IsType<AddBatchRequest>(Assert.Single(Inventory.Requests));
        Assert.Equal((milk.Id, Freezer.Id, true, ExpiryKindDto.None), (request.ProductId, request.StorageLocationId, request.SelfFrozen, request.PackageExpiryKind));
        Assert.Equal(new QuantityDto(2m, "l"), request.Quantity);
    }

    [Fact]
    public async Task Add_InTheFridge_HasNoFreezerSwitch_ShowsFieldErrors()
    {
        Inventory.FailWith = new Client.Http.ApiError(400, "inventory.validation_failed", null,
            new Dictionary<string, string[]> { ["quantity.value"] = ["must_be_positive"] }, null);
        await ShowAsync<AddBatchDialog>(p => { p.Add(d => d.Product, Summary("Milch")); p.Add(d => d.Locations, Locations); });

        Dialogs.WaitForAssertion(() => Assert.Contains("Lagerort", Dialogs.Markup, StringComparison.Ordinal));
        Assert.DoesNotContain("Selbst eingefroren", Dialogs.Markup, StringComparison.Ordinal);
        ButtonByText(Dialogs, "Speichern").Click();

        Dialogs.WaitForAssertion(() => Assert.Contains("Die Menge muss größer als 0 sein.", Dialogs.Markup, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Add_WithDate_NeedsTheKind_NoPreselection()
    {
        await ShowAsync<AddBatchDialog>(p => { p.Add(d => d.Product, Summary("Hack")); p.Add(d => d.Locations, Locations); });
        Dialogs.WaitForAssertion(() => Assert.Contains("Ablaufdatum", Dialogs.Markup, StringComparison.Ordinal));

        Dialogs.Find(".fa-date-field input").Change("24.11.2026");
        Dialogs.WaitForAssertion(() => Assert.Contains("Art des Datums", Dialogs.Markup, StringComparison.Ordinal));
        Assert.DoesNotContain(Dialogs.FindAll("label.fa-option input"), i => i.HasAttribute("checked"));
        ButtonByText(Dialogs, "Speichern").Click();

        Assert.Empty(Inventory.Calls);
        Assert.Contains("Bitte ausfüllen.", Dialogs.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Partial_SendsTheAmount_AndShowsTheServerFieldError()
    {
        var batch = Batch(Summary("Milch"), 2, ExpiryStatusDto.Soon);
        Inventory.FailWith = new Client.Http.ApiError(400, "inventory.validation_failed", null,
            new Dictionary<string, string[]> { ["quantity"] = ["must_be_less_than_remaining"] }, null);
        await ShowAsync<ConsumeDialog>(p => { p.Add(d => d.Batch, batch); p.Add(d => d.Mode, ConsumptionKindDto.Partial); });
        Dialogs.WaitForAssertion(() => Assert.Contains("Teilweise verbraucht", Dialogs.Markup, StringComparison.Ordinal));

        Dialogs.Find(".fa-quantity-input input").Change("1");
        ButtonByText(Dialogs, "Speichern").Click();

        var request = Assert.IsType<ConsumeBatchRequest>(Assert.Single(Inventory.Requests));
        Assert.Equal((ConsumptionKindDto.Partial, new QuantityDto(1m, "l")), (request.Kind, request.Quantity));
        Dialogs.WaitForAssertion(() => Assert.Contains("Teilweise heißt: weniger als der Rest.", Dialogs.Markup, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Discard_NeedsAReason_ThenSendsItWithTheVersion()
    {
        var batch = Batch(Summary("Milch"), 2, ExpiryStatusDto.Soon);
        await ShowAsync<ConsumeDialog>(p => { p.Add(d => d.Batch, batch); p.Add(d => d.Mode, ConsumptionKindDto.Discarded); });
        Dialogs.WaitForAssertion(() => Assert.Contains("verdorben", Dialogs.Markup, StringComparison.Ordinal));

        ButtonByText(Dialogs, "Speichern").Click();
        Assert.Empty(Inventory.Calls);
        Assert.Contains("Bitte ausfüllen.", Dialogs.Markup, StringComparison.Ordinal);

        Dialogs.FindAll("label.fa-option").First(l => l.TextContent.Contains("verdorben", StringComparison.Ordinal)).QuerySelector("input")!.Change(true);
        ButtonByText(Dialogs, "Speichern").Click();

        Assert.Equal($"consume:{batch.Id}@\"7\"", Assert.Single(Inventory.Calls));
        var request = Assert.IsType<ConsumeBatchRequest>(Assert.Single(Inventory.Requests));
        Assert.Equal((ConsumptionKindDto.Discarded, ConsumptionReasonDto.Spoiled, (QuantityDto?)null), (request.Kind, request.Reason, request.Quantity));
    }

    [Fact]
    public async Task Move_ThawedIntoTheFreezer_IsRefusedUpFront()
    {
        var batch = Batch(Summary("Hack"), 1, ExpiryStatusDto.Soon, state: BatchStateDto.Thawed);
        await ShowAsync<MoveBatchDialog>(p => { p.Add(d => d.Batch, batch); p.Add(d => d.Locations, Locations); p.Add(d => d.LocationId, Freezer.Id); });

        Dialogs.WaitForAssertion(() => Assert.Contains("Aufgetautes darf nicht wieder eingefroren werden.", Dialogs.Markup, StringComparison.Ordinal));
        Assert.True(Dialogs.FindAll("button").Last(b => b.TextContent.Trim() == "Verschieben").HasAttribute("disabled"));
        Assert.Empty(Inventory.Calls);
    }

    [Fact]
    public async Task Move_FromFridgeToFreezer_SaysItFreezes_AndSendsTheTarget()
    {
        var batch = Batch(Summary("Hack"), 1, ExpiryStatusDto.Soon);
        await ShowAsync<MoveBatchDialog>(p => { p.Add(d => d.Batch, batch); p.Add(d => d.Locations, Locations); p.Add(d => d.LocationId, Freezer.Id); });
        Dialogs.WaitForAssertion(() => Assert.Contains("wird eingefroren", Dialogs.Markup, StringComparison.Ordinal));

        Dialogs.FindAll("button").Last(b => b.TextContent.Trim() == "Verschieben").Click();

        Assert.Equal(Freezer.Id, Assert.IsType<MoveBatchRequest>(Assert.Single(Inventory.Requests)).StorageLocationId);
    }

    [Fact]
    public async Task Correct_NotSealed_LocksTheDate_AndSendsThePrintedValuesBack()
    {
        var batch = Batch(Summary("Milch"), 5, ExpiryStatusDto.Ok, state: BatchStateDto.Opened);
        await ShowAsync<CorrectBatchDialog>(p => { p.Add(d => d.Batch, batch); });
        Dialogs.WaitForAssertion(() => Assert.Contains("Das Datum ist gesperrt", Dialogs.Markup, StringComparison.Ordinal));

        Assert.DoesNotContain("Ablaufdatum", Dialogs.Markup, StringComparison.Ordinal);
        ButtonByText(Dialogs, "Speichern").Click();

        var request = Assert.IsType<CorrectBatchRequest>(Assert.Single(Inventory.Requests));
        Assert.Equal((batch.PackageExpiresOn, batch.PackageExpiryKind), (request.PackageExpiresOn, request.PackageExpiryKind));
    }

    [Fact]
    public async Task Leftover_PreselectsTheLocation_AndSendsTheProduct()
    {
        var milk = Summary("Suppe");
        await ShowAsync<LeftoverDialog>(p => { p.Add(d => d.Product, milk); p.Add(d => d.Locations, Locations); p.Add(d => d.LocationId, Freezer.Id); });
        Dialogs.WaitForAssertion(() => Assert.Contains("Regel „Rest“", Dialogs.Markup, StringComparison.Ordinal));

        ButtonByText(Dialogs, "Speichern").Click();

        var request = Assert.IsType<CreateLeftoverRequest>(Assert.Single(Inventory.Requests));
        Assert.Equal((milk.Id, Freezer.Id), (request.ProductId, request.StorageLocationId));
    }
}
