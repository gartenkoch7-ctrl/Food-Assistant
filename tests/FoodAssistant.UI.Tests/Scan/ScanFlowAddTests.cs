using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using FoodAssistant.Client.Http;
using FoodAssistant.Contracts.Common;
using FoodAssistant.Contracts.FoodFacts;
using FoodAssistant.Contracts.Inventory;
using FoodAssistant.UI.Scan;
using FoodAssistant.UI.Tests.Support;

namespace FoodAssistant.UI.Tests.Scan;

/// <summary>Scan mode "add": known product, unknown product with and without Open Food Facts, manual pick.</summary>
public sealed class ScanFlowAddTests : ScanFlowTestContext
{
    [Fact]
    public async Task KnownBarcode_OpensTheAddDialog_AndAddsAScannedBatch()
    {
        var milk = KnownProduct();
        var pending = Start(ScanCode.Barcode(Barcode), ScanMode.Add);

        WaitForDialogText("Lagerort");
        Click("Speichern");
        var outcome = await pending;

        var request = SingleAddRequest();
        Assert.Equal((milk.Id, Fridge.Id, BatchOriginDto.Scanned, new QuantityDto(1m, "l")), (request.ProductId, request.StorageLocationId, request.Origin, request.Quantity));
        Assert.Equal((ScanOutcomeKind.Added, "Milch", (Guid?)Inventory.NextAction.ActivityId), (outcome.Kind, outcome.ProductName, outcome.ActivityId));
        Assert.Empty(FoodFacts.Calls);
    }

    [Fact]
    public async Task KnownBarcode_PreselectsTheDefaultStorageLocation()
    {
        KnownProduct(defaultLocation: Freezer.Id);
        var pending = Start(ScanCode.Barcode(Barcode), ScanMode.Add);

        WaitForDialogText("Selbst eingefroren");
        Click("Speichern");
        await pending;

        Assert.Equal(Freezer.Id, SingleAddRequest().StorageLocationId);
    }

    [Fact]
    public async Task KnownBarcode_CancelledDialog_ChangesNothing()
    {
        KnownProduct();
        var pending = Start(ScanCode.Barcode(Barcode), ScanMode.Add);

        WaitForDialogText("Lagerort");
        Click("Abbrechen");

        Assert.Equal(ScanOutcomeKind.Cancelled, (await pending).Kind);
        Assert.Empty(Inventory.Calls);
    }

    [Fact]
    public async Task UnknownBarcode_WithSuggestion_PrefillsTheProduct_ThenAddsThePackageAmount()
    {
        FoodFacts.Suggestions[Barcode] = Suggestion(new QuantityDto(1m, "l"));
        var pending = Start(ScanCode.Barcode(Barcode), ScanMode.Add);

        WaitForDialogText("Open Food Facts");
        Assert.Equal("Hafermilch", InputFor(Dialogs, "Name").GetAttribute("value"));
        Assert.Contains("Community-Daten", Dialogs.Markup, StringComparison.Ordinal);
        Assert.Contains("https://world.openfoodfacts.org", Dialogs.Markup, StringComparison.Ordinal);
        Click("Speichern");

        WaitForDialogText("Lagerort");
        Click("Speichern");
        var outcome = await pending;

        var created = Assert.Single(Catalog.Products);
        Assert.Equal(("Hafermilch", "Oatly", true), (created.Name, created.Brand, created.FromOpenFoodFacts));
        Assert.Equal([Barcode], created.Barcodes);
        var request = SingleAddRequest();
        Assert.Equal((created.Id, new QuantityDto(1m, "l"), BatchOriginDto.Scanned), (request.ProductId, request.Quantity, request.Origin));
        Assert.Equal(ScanOutcomeKind.Added, outcome.Kind);
    }

    [Fact]
    public async Task UnknownBarcode_PackageAmountInGrams_BecomesTheDefaultUnitAndTheAmount()
    {
        FoodFacts.Suggestions[Barcode] = Suggestion(new QuantityDto(500m, "g"));
        var pending = Start(ScanCode.Barcode(Barcode), ScanMode.Add);

        WaitForDialogText("Open Food Facts");
        Click("Speichern");
        WaitForDialogText("Lagerort");
        Click("Speichern");
        await pending;

        Assert.Equal(new QuantityDto(500m, "g"), SingleAddRequest().Quantity);
    }

    [Fact]
    public async Task UnknownBarcode_WithoutSuggestion_OpensAnEmptyDialog_WithTheBarcode_AndNoAttribution()
    {
        var pending = Start(ScanCode.Barcode(Barcode), ScanMode.Add);

        WaitForDialogText("keinen Vorschlag");
        Assert.DoesNotContain("world.openfoodfacts.org", Dialogs.Markup, StringComparison.Ordinal);
        Assert.Contains(Barcode, Dialogs.Markup, StringComparison.Ordinal);
        Click("Abbrechen");

        Assert.Equal(ScanOutcomeKind.Cancelled, (await pending).Kind);
        Assert.Empty(Catalog.Products);
    }

    [Fact]
    public async Task UnknownBarcode_OpenFoodFactsUnavailable_StillLetsThePersonCreateTheProduct()
    {
        FoodFacts.FailWith = FakeAccountClient.Error(503, FoodFactsErrorCodes.Unavailable);
        var pending = Start(ScanCode.Barcode(Barcode), ScanMode.Add);

        WaitForDialogText("keinen Vorschlag");
        Click("Abbrechen");

        Assert.Equal(ScanOutcomeKind.Cancelled, (await pending).Kind);
        Assert.Single(FoodFacts.Calls);
    }

    [Fact]
    public async Task UnknownBarcode_ProductCreated_ButAddCancelled_ReportsTheProductOnly()
    {
        FoodFacts.Suggestions[Barcode] = Suggestion();
        var pending = Start(ScanCode.Barcode(Barcode), ScanMode.Add);

        WaitForDialogText("Open Food Facts");
        Click("Speichern");
        WaitForDialogText("Lagerort");
        Click("Abbrechen");
        var outcome = await pending;

        Assert.Equal((ScanOutcomeKind.ProductCreated, "Hafermilch"), (outcome.Kind, outcome.ProductName));
        Assert.Single(Catalog.Products);
        Assert.Empty(Inventory.Calls);
    }

    [Fact]
    public async Task WithoutStorageLocation_FailsWithAHint()
    {
        KnownProduct();
        Catalog.Locations.Clear();

        var outcome = await Start(ScanCode.Barcode(Barcode), ScanMode.Add);

        Assert.Equal((ScanOutcomeKind.Failed, "scan.no_location"), (outcome.Kind, outcome.Error!.ErrorCode));
    }

    [Fact]
    public async Task CatalogError_IsReported_NotTreatedAsUnknown()
    {
        Catalog.FailWith = FakeAccountClient.Error(500, "common.internal_error");

        var outcome = await Start(ScanCode.Barcode(Barcode), ScanMode.Add);

        Assert.Equal((ScanOutcomeKind.Failed, 500), (outcome.Kind, outcome.Error!.Status));
        Assert.Empty(FoodFacts.Calls);
    }

    [Fact]
    public async Task PickedProduct_GoesThroughTheAddDialogToo()
    {
        var milk = KnownProduct();
        var pending = Dialogs.InvokeAsync(() => Flow.HandleProductAsync(milk.Id, ScanMode.Add, Ct));

        WaitForDialogText("Lagerort");
        Click("Speichern");

        Assert.Equal(ScanOutcomeKind.Added, (await pending).Kind);
        var request = SingleAddRequest();
        Assert.Equal((milk.Id, BatchOriginDto.Manual), (request.ProductId, request.Origin));
    }

    [Fact]
    public async Task BatchLabel_OpensTheBatchPage()
    {
        var batchId = Guid.CreateVersion7();

        var outcome = await Flow.HandleAsync(ScanCode.Batch(batchId), ScanMode.Add, Ct);

        Assert.Equal(ScanOutcomeKind.Navigated, outcome.Kind);
        Assert.EndsWith($"/inventory/batches/{batchId:D}", Services.GetRequiredService<NavigationManager>().Uri, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnknownText_IsNotLookedUp()
    {
        var outcome = await Flow.HandleAsync(ScanCode.Unknown, ScanMode.Add, Ct);

        Assert.Equal(ScanOutcomeKind.UnknownCode, outcome.Kind);
        Assert.Empty(Catalog.Calls);
    }

    [Fact]
    public async Task WithoutHousehold_Fails()
    {
        SignOut();

        var outcome = await Flow.HandleAsync(ScanCode.Barcode(Barcode), ScanMode.Add, Ct);

        Assert.Equal((ScanOutcomeKind.Failed, "scan.no_household"), (outcome.Kind, outcome.Error!.ErrorCode));
    }
}
