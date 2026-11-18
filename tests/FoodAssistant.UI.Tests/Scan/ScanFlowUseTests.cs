using Bunit;
using FoodAssistant.Contracts.Common;
using FoodAssistant.Contracts.Inventory;
using FoodAssistant.UI.Scan;
using FoodAssistant.UI.Tests.Support;

namespace FoodAssistant.UI.Tests.Scan;

/// <summary>Scan mode "used": the F6 rule, undo, and changing the amount afterwards.</summary>
public sealed class ScanFlowUseTests : ScanFlowTestContext
{
    [Fact]
    public async Task CountedProduct_TakesOnePiece_AndOffersUndo()
    {
        var eggs = KnownProduct("piece");
        StockOf(eggs, PieceBatch(eggs, 6m));

        var outcome = await Flow.HandleAsync(ScanCode.Barcode(Barcode), ScanMode.Use, Ct);

        var request = Assert.IsType<ConsumeProductRequest>(Assert.Single(Inventory.Requests));
        Assert.Equal((ConsumptionKindDto.Partial, new QuantityDto(1m, "piece")), (request.Kind, request.Quantity));
        Assert.Equal((ScanOutcomeKind.Used, "Milch", (Guid?)Inventory.NextAction.ActivityId), (outcome.Kind, outcome.ProductName, outcome.ActivityId));
        Assert.Contains($"consume-product:{eggs.Id}", Inventory.Calls);
    }

    [Fact]
    public async Task LastPiece_IsUsedUp()
    {
        var eggs = KnownProduct("piece");
        StockOf(eggs, PieceBatch(eggs, 1m));

        await Flow.HandleAsync(ScanCode.Barcode(Barcode), ScanMode.Use, Ct);

        var request = Assert.IsType<ConsumeProductRequest>(Assert.Single(Inventory.Requests));
        Assert.Equal((ConsumptionKindDto.UsedUp, (QuantityDto?)null), (request.Kind, request.Quantity));
    }

    [Fact]
    public async Task VolumeProduct_UsesUpTheWholeEarliestBatch()
    {
        var milk = KnownProduct("l");
        StockOf(milk, PieceBatch(milk, 2m, unit: "l"));

        await Flow.HandleAsync(ScanCode.Barcode(Barcode), ScanMode.Use, Ct);

        var request = Assert.IsType<ConsumeProductRequest>(Assert.Single(Inventory.Requests));
        Assert.Equal((ConsumptionKindDto.UsedUp, (QuantityDto?)null), (request.Kind, request.Quantity));
    }

    [Fact]
    public async Task ProductWithoutStock_UsesNothing()
    {
        KnownProduct();

        var outcome = await Flow.HandleAsync(ScanCode.Barcode(Barcode), ScanMode.Use, Ct);

        Assert.Equal((ScanOutcomeKind.NotInStock, "Milch"), (outcome.Kind, outcome.ProductName));
        Assert.Empty(Inventory.Requests);
    }

    [Fact]
    public async Task ProductWithAnEmptyBatchList_UsesNothing()
    {
        var milk = KnownProduct();
        StockOf(milk);

        var outcome = await Flow.HandleAsync(ScanCode.Barcode(Barcode), ScanMode.Use, Ct);

        Assert.Equal(ScanOutcomeKind.NotInStock, outcome.Kind);
        Assert.Empty(Inventory.Requests);
    }

    [Fact]
    public async Task UnknownBarcode_CreatesNothing_AndAsksNoOneElse()
    {
        var outcome = await Flow.HandleAsync(ScanCode.Barcode(Barcode), ScanMode.Use, Ct);

        Assert.Equal(ScanOutcomeKind.UnknownProduct, outcome.Kind);
        Assert.Empty(FoodFacts.Calls);
        Assert.DoesNotContain("create-product", Catalog.Calls);
    }

    [Fact]
    public async Task ConsumeFailure_IsReported()
    {
        var eggs = KnownProduct("piece");
        StockOf(eggs, PieceBatch(eggs, 6m));
        Inventory.FailWith = FakeAccountClient.Error(409, "inventory.not_enough_stock");

        var outcome = await Flow.HandleAsync(ScanCode.Barcode(Barcode), ScanMode.Use, Ct);

        Assert.Equal((ScanOutcomeKind.Failed, "inventory.not_enough_stock"), (outcome.Kind, outcome.Error!.ErrorCode));
    }

    [Fact]
    public async Task Undo_RevertsTheActivity()
    {
        var eggs = KnownProduct("piece");
        StockOf(eggs, PieceBatch(eggs, 6m));
        var used = await Flow.HandleAsync(ScanCode.Barcode(Barcode), ScanMode.Use, Ct);

        var undone = await Flow.UndoAsync(used, Ct);

        Assert.Equal(ScanOutcomeKind.Undone, undone.Kind);
        Assert.Contains($"undo:{used.ActivityId}", Inventory.Calls);
    }

    [Fact]
    public async Task Undo_NotPossible_IsReported()
    {
        var used = new ScanOutcome(ScanOutcomeKind.Used, "Milch", Guid.CreateVersion7(), Guid.CreateVersion7());
        Inventory.FailWith = FakeAccountClient.Error(409, "inventory.undo_not_possible");

        var undone = await Flow.UndoAsync(used, Ct);

        Assert.Equal((ScanOutcomeKind.Failed, "inventory.undo_not_possible"), (undone.Kind, undone.Error!.ErrorCode));
    }

    [Fact]
    public async Task ChangeAmount_UndoesTheAutomaticAmount_ThenConsumesTheEnteredOne()
    {
        var eggs = KnownProduct("piece");
        var batch = PieceBatch(eggs, 6m);
        StockOf(eggs, batch);
        var used = await Flow.HandleAsync(ScanCode.Barcode(Barcode), ScanMode.Use, Ct);
        Inventory.Calls.Clear();
        Inventory.Requests.Clear();
        var newActivity = Guid.CreateVersion7();
        Inventory.NextAction = new InventoryActionResponse([], [], newActivity);

        var pending = Dialogs.InvokeAsync(() => Flow.ChangeAmountAsync(used, Ct));
        WaitForDialogText("Teilweise verbraucht");
        Dialogs.Find(".fa-quantity-input input").Change("4");
        Click("Speichern");
        var changed = await pending;

        Assert.StartsWith("undo:", Inventory.Calls[0], StringComparison.Ordinal);
        Assert.Contains($"consume:{batch.Id}@\"7\"", Inventory.Calls);
        var request = Assert.IsType<ConsumeBatchRequest>(Inventory.Requests[^1]);
        Assert.Equal((ConsumptionKindDto.Partial, new QuantityDto(4m, "piece")), (request.Kind, request.Quantity));
        Assert.Equal((ScanOutcomeKind.Used, (Guid?)newActivity), (changed.Kind, changed.ActivityId));
    }

    [Fact]
    public async Task ChangeAmount_Cancelled_LeavesTheStockAsBeforeTheScan()
    {
        var eggs = KnownProduct("piece");
        StockOf(eggs, PieceBatch(eggs, 6m));
        var used = await Flow.HandleAsync(ScanCode.Barcode(Barcode), ScanMode.Use, Ct);

        var pending = Dialogs.InvokeAsync(() => Flow.ChangeAmountAsync(used, Ct));
        WaitForDialogText("Teilweise verbraucht");
        Click("Abbrechen");

        Assert.Equal(ScanOutcomeKind.Undone, (await pending).Kind);
        Assert.DoesNotContain(Inventory.Calls, c => c.StartsWith("consume:", StringComparison.Ordinal));
    }

    [Fact]
    public async Task PickedProduct_IsUsedLikeAScannedOne()
    {
        var eggs = KnownProduct("piece");
        StockOf(eggs, PieceBatch(eggs, 3m));

        var outcome = await Flow.HandleProductAsync(eggs.Id, ScanMode.Use, Ct);

        Assert.Equal(ScanOutcomeKind.Used, outcome.Kind);
        Assert.Empty(FoodFacts.Calls);
    }
}
