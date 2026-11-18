using System.Net;
using FoodAssistant.Contracts.Common;
using FoodAssistant.Contracts.Households;
using FoodAssistant.Contracts.Inventory;
using FoodAssistant.Infrastructure.Activity;
using FoodAssistant.Infrastructure.Inventory;
using FoodAssistant.Server.Tests.Auth;
using static FoodAssistant.Server.Tests.Inventory.InventoryApi;

namespace FoodAssistant.Server.Tests.Inventory;

/// <summary>Undo of inventory actions within 24 hours (F4).</summary>
public sealed class UndoEndpointTests(PostgresContainerFixture postgres)
{
    [Fact]
    public async Task Undo_AfterUsedUp_RestoresBatchWithSameId_MarksEventAndActivity()
    {
        await using var app = await InventoryApp.StartAsync(postgres);
        var batch = await AddAsync(app, Add(app.Milk.Id, app.Fridge.Id, amount: 2m, date: app.Today().AddDays(3)));
        var consumed = await ActAsync(app.Owner, HttpMethod.Post, $"{app.Inventory}/batches/{batch.Id}/consume",
            new ConsumeBatchRequest(ConsumptionKindDto.UsedUp, null, null));
        app.StockHandler.Calls.Clear();

        var undone = await UndoAsync(app, consumed.ActivityId);
        using var twice = await TryUndoAsync(app, consumed.ActivityId);

        var restored = Assert.Single(undone.Batches);
        Assert.Equal((batch.Id, batch.Quantity, batch.ExpiresOn, batch.State), (restored.Id, restored.Quantity, restored.ExpiresOn, restored.State));
        Assert.Equal(restored.Version, (await GetBatchAsync(app, batch.Id)).Version);
        Assert.NotNull(Assert.Single(await app.RowsAsync<ConsumptionEvent>()).UndoneAt);
        var activities = await app.RowsAsync<HouseholdActivity>();
        Assert.NotNull(activities.Single(a => a.Id == consumed.ActivityId).UndoneAt);
        Assert.Equal("undone", activities.Single(a => a.Id == undone.ActivityId).Kind);
        Assert.Single(app.StockHandler.Calls);
        await TestClient.AssertProblemAsync(twice, HttpStatusCode.Conflict, InventoryErrorCodes.UndoNotPossible);
    }

    [Fact]
    public async Task Undo_AfterOpenAndMove_RestoresStateAndLocation()
    {
        await using var app = await InventoryApp.StartAsync(postgres);
        var date = app.Today().AddDays(8);
        var batch = await AddAsync(app, Add(app.Milk.Id, app.Fridge.Id, date: date));
        var url = $"{app.Inventory}/batches/{batch.Id}";
        var opened = await ActAsync(app.Owner, HttpMethod.Post, $"{url}/open", ifMatch: batch.Version);

        var reopened = Assert.Single((await UndoAsync(app, opened.ActivityId)).Batches);
        var moved = await ActAsync(app.Owner, HttpMethod.Post, $"{url}/move", new MoveBatchRequest(app.Pantry.Id), reopened.Version);
        var back = Assert.Single((await UndoAsync(app, moved.ActivityId)).Batches);

        Assert.Equal((BatchStateDto.Sealed, (DateOnly?)null, date), (reopened.State, reopened.OpenedOn, reopened.ExpiresOn));
        Assert.Equal((app.Fridge.Id, StorageTypeDto.Chilled), (back.StorageLocationId, back.StorageType));
    }

    [Fact]
    public async Task Undo_AfterAdd_RemovesTheBatch()
    {
        await using var app = await InventoryApp.StartAsync(postgres);
        using var response = await app.Owner.PostAsync($"{app.Inventory}/batches", Add(app.Milk.Id, app.Fridge.Id));
        var added = await TestClient.ReadAsync<InventoryActionResponse>(response, HttpStatusCode.Created);

        var undone = await UndoAsync(app, added.ActivityId);

        Assert.Equal([added.Batches[0].Id], undone.RemovedBatchIds);
        Assert.Empty(await app.RowsAsync<StockBatchRecord>());
    }

    [Fact]
    public async Task Undo_BatchChangedSince_409()
    {
        await using var app = await InventoryApp.StartAsync(postgres);
        var batch = await AddAsync(app, Add(app.Milk.Id, app.Fridge.Id, amount: 2m));
        var url = $"{app.Inventory}/batches/{batch.Id}";
        var partly = await ActAsync(app.Owner, HttpMethod.Post, $"{url}/consume",
            new ConsumeBatchRequest(ConsumptionKindDto.Partial, new QuantityDto(1m, "l"), null));
        await ActAsync(app.Owner, HttpMethod.Post, $"{url}/open", ifMatch: partly.Batches[0].Version);

        using var refused = await TryUndoAsync(app, partly.ActivityId);

        await TestClient.AssertProblemAsync(refused, HttpStatusCode.Conflict, InventoryErrorCodes.UndoNotPossible);
        Assert.Null(Assert.Single(await app.RowsAsync<ConsumptionEvent>()).UndoneAt);
    }

    [Fact]
    public async Task Undo_After24Hours_409_UndoOfUndo_409()
    {
        await using var app = await InventoryApp.StartAsync(postgres);
        var batch = await AddAsync(app, Add(app.Milk.Id, app.Fridge.Id));
        var url = $"{app.Inventory}/batches/{batch.Id}";
        var opened = await ActAsync(app.Owner, HttpMethod.Post, $"{url}/open", ifMatch: batch.Version);
        var reopened = await UndoAsync(app, opened.ActivityId);
        var openedAgain = await ActAsync(app.Owner, HttpMethod.Post, $"{url}/open", ifMatch: reopened.Batches[0].Version);

        using var undoOfUndo = await TryUndoAsync(app, reopened.ActivityId);
        app.Time.Advance(TimeSpan.FromHours(24) + TimeSpan.FromMinutes(1));
        using var late = await TryUndoAsync(app, openedAgain.ActivityId);

        await TestClient.AssertProblemAsync(undoOfUndo, HttpStatusCode.Conflict, InventoryErrorCodes.UndoNotPossible);
        await TestClient.AssertProblemAsync(late, HttpStatusCode.Conflict, InventoryErrorCodes.UndoNotPossible);
    }

    [Fact]
    public async Task Undo_Restricted_403_ForeignOrUnknownActivity_404()
    {
        await using var app = await InventoryApp.StartAsync(postgres);
        var batch = await AddAsync(app, Add(app.Milk.Id, app.Fridge.Id));
        var opened = await ActAsync(app.Owner, HttpMethod.Post, $"{app.Inventory}/batches/{batch.Id}/open", ifMatch: batch.Version);
        using var kid = await app.AddMemberAsync(HouseholdRole.Restricted, "kid");
        var otherId = await Scenario.CreateHouseholdAsync(app.Owner, "Other");

        using var restricted = await SendAsync(kid, HttpMethod.Post, $"{app.Inventory}/activities/{opened.ActivityId}/undo");
        using var foreign = await SendAsync(app.Owner, HttpMethod.Post, $"/api/v1/households/{otherId}/inventory/activities/{opened.ActivityId}/undo");
        using var unknown = await TryUndoAsync(app, Guid.CreateVersion7());

        Assert.Equal(HttpStatusCode.Forbidden, restricted.StatusCode);
        await TestClient.AssertProblemAsync(foreign, HttpStatusCode.NotFound, InventoryErrorCodes.ActivityNotFound);
        await TestClient.AssertProblemAsync(unknown, HttpStatusCode.NotFound, InventoryErrorCodes.ActivityNotFound);
    }
}
