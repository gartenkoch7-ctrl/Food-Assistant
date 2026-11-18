using System.Net;
using FoodAssistant.Contracts.Catalog;
using FoodAssistant.Contracts.Common;
using FoodAssistant.Contracts.Households;
using FoodAssistant.Contracts.Inventory;
using FoodAssistant.Domain.Expiry;
using FoodAssistant.Domain.Inventory;
using FoodAssistant.Infrastructure.Inventory;
using FoodAssistant.Server.Tests.Auth;
using FoodAssistant.Server.Tests.Support;
using static FoodAssistant.Server.Tests.Inventory.InventoryApi;

namespace FoodAssistant.Server.Tests.Inventory;

/// <summary>Using up, partly using and discarding batches; FEFO over a product (B2.2).</summary>
public sealed class ConsumptionEndpointTests(PostgresContainerFixture postgres)
{
    [Fact]
    public async Task UsedUp_DeletesBatch_RecordsEventWithStatus_CallsHook()
    {
        await using var app = await InventoryApp.StartAsync(postgres);
        var expired = await AddAsync(app, Add(app.Milk.Id, app.Fridge.Id, date: app.Today().AddDays(-1)));
        app.StockHandler.Calls.Clear();

        var result = await ActAsync(app.Owner, HttpMethod.Post, $"{app.Inventory}/batches/{expired.Id}/consume",
            new ConsumeBatchRequest(ConsumptionKindDto.UsedUp, null, null), expired.Version);

        Assert.Empty(result.Batches);
        Assert.Equal([expired.Id], result.RemovedBatchIds);
        using var gone = await app.Owner.GetAsync($"{app.Inventory}/batches/{expired.Id}");
        Assert.Equal(HttpStatusCode.NotFound, gone.StatusCode);
        var consumption = Assert.Single(await app.RowsAsync<ConsumptionEvent>());
        Assert.Equal((ConsumptionKind.UsedUp, 1m, "l", ExpiryStatus.Expired), (consumption.Kind, consumption.QuantityValue, consumption.QuantityUnitCode, consumption.ExpiryStatusAtEvent));
        Assert.Equal((expired.Id, app.Today(), (ConsumptionReason?)null), (consumption.BatchId, consumption.OccurredOn, consumption.Reason));
        Assert.NotNull(consumption.UserId);
        Assert.Single(app.StockHandler.Calls);
    }

    [Fact]
    public async Task Partial_ConvertsUnits_KeepsRest()
    {
        await using var app = await InventoryApp.StartAsync(postgres);
        var batch = await AddAsync(app, Add(app.Milk.Id, app.Fridge.Id, amount: 1m, unit: "kg"));
        var url = $"{app.Inventory}/batches/{batch.Id}/consume";

        var rest = Assert.Single((await ActAsync(app.Owner, HttpMethod.Post, url,
            new ConsumeBatchRequest(ConsumptionKindDto.Partial, new QuantityDto(500m, "g"), null))).Batches);
        using var all = await SendAsync(app.Owner, HttpMethod.Post, url,
            new ConsumeBatchRequest(ConsumptionKindDto.Partial, new QuantityDto(0.5m, "kg"), null));
        using var otherUnit = await SendAsync(app.Owner, HttpMethod.Post, url,
            new ConsumeBatchRequest(ConsumptionKindDto.Partial, new QuantityDto(1m, "l"), null));
        using var missing = await SendAsync(app.Owner, HttpMethod.Post, url,
            new ConsumeBatchRequest(ConsumptionKindDto.Partial, null, null));

        Assert.Equal(new QuantityDto(0.5m, "kg"), rest.Quantity);
        Assert.Equal(BatchStateDto.Sealed, rest.State);
        await TestClient.AssertProblemAsync(all, HttpStatusCode.BadRequest, InventoryErrorCodes.ValidationFailed);
        await TestClient.AssertProblemAsync(otherUnit, HttpStatusCode.UnprocessableEntity, InventoryErrorCodes.UnitMismatch);
        await TestClient.AssertProblemAsync(missing, HttpStatusCode.BadRequest, InventoryErrorCodes.ValidationFailed);
        var consumption = Assert.Single(await app.RowsAsync<ConsumptionEvent>());
        Assert.Equal((ConsumptionKind.Partial, 0.5m, "kg"), (consumption.Kind, consumption.QuantityValue, consumption.QuantityUnitCode));
    }

    [Theory]
    [InlineData(999.9, 0, true)]
    [InlineData(1.5, 0.998, false)]
    public async Task Partial_RestBeyondThreeDecimals_TakesUpToNextThousandth(double gramsValue, double restValue, bool emptied)
    {
        var (grams, restKg) = ((decimal)gramsValue, (decimal)restValue);
        await using var app = await InventoryApp.StartAsync(postgres);
        var batch = await AddAsync(app, Add(app.Milk.Id, app.Fridge.Id, amount: 1m, unit: "kg"));

        var result = await ActAsync(app.Owner, HttpMethod.Post, $"{app.Inventory}/batches/{batch.Id}/consume",
            new ConsumeBatchRequest(ConsumptionKindDto.Partial, new QuantityDto(grams, "g"), null));

        Assert.Equal(emptied, result.RemovedBatchIds.Contains(batch.Id));
        Assert.Equal(emptied ? [] : [restKg], result.Batches.Select(b => b.Quantity.Value));
        Assert.Equal(1m - restKg, Assert.Single(await app.RowsAsync<ConsumptionEvent>()).QuantityValue);
    }

    [Fact]
    public async Task Discarded_NeedsReason_TakesRestOrLess()
    {
        await using var app = await InventoryApp.StartAsync(postgres);
        var batch = await AddAsync(app, Add(app.Milk.Id, app.Fridge.Id, amount: 2m));
        var url = $"{app.Inventory}/batches/{batch.Id}/consume";

        using var noReason = await SendAsync(app.Owner, HttpMethod.Post, url, new ConsumeBatchRequest(ConsumptionKindDto.Discarded, null, null));
        using var tooMuch = await SendAsync(app.Owner, HttpMethod.Post, url,
            new ConsumeBatchRequest(ConsumptionKindDto.Discarded, new QuantityDto(3m, "l"), ConsumptionReasonDto.Spoiled));
        var part = await ActAsync(app.Owner, HttpMethod.Post, url,
            new ConsumeBatchRequest(ConsumptionKindDto.Discarded, new QuantityDto(0.5m, "l"), ConsumptionReasonDto.Spoiled));
        var rest = await ActAsync(app.Owner, HttpMethod.Post, url,
            new ConsumeBatchRequest(ConsumptionKindDto.Discarded, null, ConsumptionReasonDto.TooMuchCooked));

        await TestClient.AssertProblemAsync(noReason, HttpStatusCode.BadRequest, InventoryErrorCodes.ValidationFailed);
        await TestClient.AssertProblemAsync(tooMuch, HttpStatusCode.BadRequest, InventoryErrorCodes.ValidationFailed);
        Assert.Equal(new QuantityDto(1.5m, "l"), Assert.Single(part.Batches).Quantity);
        Assert.Equal([batch.Id], rest.RemovedBatchIds);
        var events = (await app.RowsAsync<ConsumptionEvent>()).OrderBy(e => e.QuantityValue).ToList();
        Assert.Equal(
            [(ConsumptionKind.Discarded, ConsumptionReason.Spoiled, 0.5m), (ConsumptionKind.Discarded, ConsumptionReason.TooMuchCooked, 1.5m)],
            events.Select(e => (e.Kind, e.Reason!.Value, e.QuantityValue)));
    }

    [Fact]
    public async Task ConsumeProduct_FollowsFefo_BatchesWithoutDateLast()
    {
        await using var app = await InventoryApp.StartAsync(postgres);
        var today = app.Today();
        var later = await AddAsync(app, Add(app.Milk.Id, app.Fridge.Id, date: today.AddDays(5)));
        var undated = await AddAsync(app, Add(app.Milk.Id, app.Pantry.Id));
        var soon = await AddAsync(app, Add(app.Milk.Id, app.Fridge.Id, date: today.AddDays(1)));
        var url = $"{app.Inventory}/products/{app.Milk.Id}/consume";

        var result = await ActAsync(app.Owner, HttpMethod.Post, url,
            new ConsumeProductRequest(ConsumptionKindDto.UsedUp, new QuantityDto(2500m, "ml"), null));
        using var shortfall = await SendAsync(app.Owner, HttpMethod.Post, url,
            new ConsumeProductRequest(ConsumptionKindDto.Partial, new QuantityDto(1m, "l"), null));

        Assert.Equal([soon.Id, later.Id], result.RemovedBatchIds);
        var left = Assert.Single(result.Batches);
        Assert.Equal((undated.Id, new QuantityDto(0.5m, "l")), (left.Id, left.Quantity));
        await TestClient.AssertProblemAsync(shortfall, HttpStatusCode.UnprocessableEntity, InventoryErrorCodes.NotEnoughStock);
        var events = await app.RowsAsync<ConsumptionEvent>();
        Assert.Equal(3, events.Count);
        Assert.Equal(ExpiryStatus.Soon, events.Single(e => e.BatchId == soon.Id).ExpiryStatusAtEvent);
        Assert.Equal((ConsumptionKind.Partial, ExpiryStatus.NoDate), events.Where(e => e.BatchId == undated.Id).Select(e => (e.Kind, e.ExpiryStatusAtEvent)).Single());
    }

    [Fact]
    public async Task ConsumeProduct_WithoutAmount_TakesEarliestBatch_EmptyStock422()
    {
        await using var app = await InventoryApp.StartAsync(postgres);
        var today = app.Today();
        var later = await AddAsync(app, Add(app.Milk.Id, app.Fridge.Id, date: today.AddDays(9)));
        var earliest = await AddAsync(app, Add(app.Milk.Id, app.Fridge.Id, date: today.AddDays(2)));
        var url = $"{app.Inventory}/products/{app.Milk.Id}/consume";
        var request = new ConsumeProductRequest(ConsumptionKindDto.Discarded, null, ConsumptionReasonDto.Expired);

        var first = await ActAsync(app.Owner, HttpMethod.Post, url, request);
        var second = await ActAsync(app.Owner, HttpMethod.Post, url, request);
        using var empty = await SendAsync(app.Owner, HttpMethod.Post, url, request);

        Assert.Equal([earliest.Id], first.RemovedBatchIds);
        Assert.Equal([later.Id], second.RemovedBatchIds);
        await TestClient.AssertProblemAsync(empty, HttpStatusCode.UnprocessableEntity, InventoryErrorCodes.NotEnoughStock);
    }

    [Fact]
    public async Task FailingStockHandler_DoesNotFailTheAction()
    {
        await using var app = await InventoryApp.StartAsync(postgres);
        var batch = await AddAsync(app, Add(app.Milk.Id, app.Fridge.Id));
        app.StockHandler.Fail = true;

        var result = await ActAsync(app.Owner, HttpMethod.Post, $"{app.Inventory}/batches/{batch.Id}/consume",
            new ConsumeBatchRequest(ConsumptionKindDto.UsedUp, null, null));

        Assert.Equal([batch.Id], result.RemovedBatchIds);
        Assert.Single(await app.RowsAsync<ConsumptionEvent>());
    }

    [Fact]
    public async Task StaleIfMatch_412_Restricted_403_ForeignHousehold_404()
    {
        await using var app = await InventoryApp.StartAsync(postgres);
        var batch = await AddAsync(app, Add(app.Milk.Id, app.Fridge.Id));
        using var kid = await app.AddMemberAsync(HouseholdRole.Restricted, "kid");
        var otherId = await Scenario.CreateHouseholdAsync(app.Owner, "Other");
        var otherFridge = await HouseholdDataSeed.StorageLocationAsync(app.Services, otherId);
        var otherProduct = await HouseholdDataSeed.ProductAsync(app.Services, otherId, "Bread");
        var foreign = await HouseholdDataSeed.BatchAsync(app.Services, otherId, otherProduct.Id, otherFridge.Id);
        var usedUp = new ConsumeBatchRequest(ConsumptionKindDto.UsedUp, null, null);
        var product = new ConsumeProductRequest(ConsumptionKindDto.UsedUp, null, null);

        using var stale = await SendAsync(app.Owner, HttpMethod.Post, $"{app.Inventory}/batches/{batch.Id}/consume", usedUp, batch.Version + 1);
        using var kidBatch = await SendAsync(kid, HttpMethod.Post, $"{app.Inventory}/batches/{batch.Id}/consume", usedUp);
        using var kidProduct = await SendAsync(kid, HttpMethod.Post, $"{app.Inventory}/products/{app.Milk.Id}/consume", product);
        using var foreignBatch = await SendAsync(app.Owner, HttpMethod.Post, $"{app.Inventory}/batches/{foreign.Id}/consume", usedUp);
        using var foreignProduct = await SendAsync(app.Owner, HttpMethod.Post, $"{app.Inventory}/products/{otherProduct.Id}/consume", product);

        await TestClient.AssertProblemAsync(stale, HttpStatusCode.PreconditionFailed, ErrorCodes.PreconditionFailed);
        Assert.Equal(HttpStatusCode.Forbidden, kidBatch.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, kidProduct.StatusCode);
        await TestClient.AssertProblemAsync(foreignBatch, HttpStatusCode.NotFound, InventoryErrorCodes.BatchNotFound);
        await TestClient.AssertProblemAsync(foreignProduct, HttpStatusCode.NotFound, CatalogErrorCodes.ProductNotFound);
        Assert.Empty(await app.RowsAsync<ConsumptionEvent>());
    }
}
