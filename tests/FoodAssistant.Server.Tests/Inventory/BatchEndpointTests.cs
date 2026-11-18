using System.Net;
using FoodAssistant.Contracts.Catalog;
using FoodAssistant.Contracts.Common;
using FoodAssistant.Contracts.Inventory;
using FoodAssistant.Domain.Inventory;
using FoodAssistant.Server.Tests.Auth;
using static FoodAssistant.Server.Tests.Inventory.InventoryApi;

namespace FoodAssistant.Server.Tests.Inventory;

/// <summary>Adding, correcting, opening and moving batches (B2.2).</summary>
public sealed class BatchEndpointTests(PostgresContainerFixture postgres)
{
    private static readonly ShelfLifeRule Rule = new() { DaysAfterOpening = 3, DaysFrozen = 90, DaysAfterThawing = 1 };

    [Theory]
    [InlineData(false, StorageTypeDto.Chilled)]
    [InlineData(true, StorageTypeDto.Ambient)]
    public async Task Add_InFridgeOrPantry_IsSealedWithPackageDate(bool pantry, StorageTypeDto expectedType)
    {
        await using var app = await InventoryApp.StartAsync(postgres);
        var date = app.Today().AddDays(5);
        var location = pantry ? app.Pantry : app.Fridge;

        using var response = await app.Owner.PostAsync($"{app.Inventory}/batches", Add(app.Milk.Id, location.Id, date: date));

        var result = await TestClient.ReadAsync<InventoryActionResponse>(response, HttpStatusCode.Created);
        var batch = Assert.Single(result.Batches);
        Assert.Equal($"batches/{batch.Id}", response.Headers.Location!.ToString());
        Assert.Equal((expectedType, BatchStateDto.Sealed, date, ExpiryKindDto.BestBefore), (batch.StorageType, batch.State, batch.ExpiresOn, batch.ExpiryKind));
        Assert.Equal((ExpiryStatusDto.Ok, 5), (batch.Status, batch.DaysUntilExpiry));
        Assert.Equal(new QuantityDto(1m, "l"), batch.Quantity);
        Assert.Contains(app.StockHandler.Calls, call => call.HouseholdId == app.HouseholdId && call.ProductIds.SequenceEqual([app.Milk.Id]));

        using var get = await app.Owner.GetAsync($"{app.Inventory}/batches/{batch.Id}");
        Assert.Equal($"\"{batch.Version}\"", get.Headers.ETag!.Tag);
    }

    [Fact]
    public async Task Add_InFreezerWithDate_IsBoughtFrozenFood()
    {
        await using var app = await InventoryApp.StartAsync(postgres);
        app.Rules.Set(StorageType.Frozen, Rule);
        var date = app.Today().AddDays(200);

        var batch = await AddAsync(app, Add(app.Milk.Id, app.Freezer.Id, date: date));

        Assert.Equal((StorageTypeDto.Frozen, BatchStateDto.Sealed, date, ExpiryKindDto.BestBefore), (batch.StorageType, batch.State, batch.ExpiresOn, batch.ExpiryKind));
        Assert.Null(batch.FrozenOn);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Add_InFreezerWithoutDateOrSelfFrozen_IsFrozenToday(bool withDate)
    {
        await using var app = await InventoryApp.StartAsync(postgres);
        app.Rules.Set(StorageType.Frozen, Rule);
        var today = app.Today();
        DateOnly? date = withDate ? today.AddDays(4) : null;

        var batch = await AddAsync(app, Add(app.Milk.Id, app.Freezer.Id, date: date, selfFrozen: withDate));

        Assert.Equal((StorageTypeDto.Frozen, BatchStateDto.Frozen, today), (batch.StorageType, batch.State, batch.FrozenOn));
        Assert.Equal((today.AddDays(90), ExpiryKindDto.UseBy), (batch.ExpiresOn, batch.ExpiryKind));
        Assert.Equal(date, batch.PackageExpiresOn);
    }

    [Fact]
    public async Task Add_InvalidInput_Returns400WithFields()
    {
        await using var app = await InventoryApp.StartAsync(postgres);
        var request = Add(app.Milk.Id, app.Fridge.Id, amount: 0m) with { PackageExpiryKind = ExpiryKindDto.UseBy, Origin = BatchOriginDto.Leftover };

        using var response = await app.Owner.PostAsync($"{app.Inventory}/batches", request);

        await TestClient.AssertProblemAsync(response, HttpStatusCode.BadRequest, InventoryErrorCodes.ValidationFailed);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("quantity.value", body, StringComparison.Ordinal);
        Assert.Contains("packageExpiryKind", body, StringComparison.Ordinal);
        Assert.Contains("origin", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Correct_Sealed_ChangesAmountAndDate_OpenedIsDateLocked()
    {
        await using var app = await InventoryApp.StartAsync(postgres);
        var batch = await AddAsync(app, Add(app.Milk.Id, app.Fridge.Id, date: app.Today().AddDays(5)));
        var url = $"{app.Inventory}/batches/{batch.Id}";
        var newDate = app.Today().AddDays(7);

        var corrected = Assert.Single((await ActAsync(app.Owner, HttpMethod.Put, url,
            new CorrectBatchRequest(new QuantityDto(500m, "ml"), newDate, ExpiryKindDto.UseBy), batch.Version)).Batches);
        var opened = Assert.Single((await ActAsync(app.Owner, HttpMethod.Post, $"{url}/open", ifMatch: corrected.Version)).Batches);
        using var locked = await SendAsync(app.Owner, HttpMethod.Put, url,
            new CorrectBatchRequest(new QuantityDto(1m, "l"), newDate.AddDays(1), ExpiryKindDto.UseBy), opened.Version);
        using var mismatch = await SendAsync(app.Owner, HttpMethod.Put, url,
            new CorrectBatchRequest(new QuantityDto(1m, "kg"), newDate, ExpiryKindDto.UseBy), opened.Version);
        var amountOnly = await ActAsync(app.Owner, HttpMethod.Put, url,
            new CorrectBatchRequest(new QuantityDto(0.25m, "l"), newDate, ExpiryKindDto.UseBy), opened.Version);

        Assert.Equal((new QuantityDto(500m, "ml"), newDate, ExpiryKindDto.UseBy), (corrected.Quantity, corrected.ExpiresOn, corrected.ExpiryKind));
        await TestClient.AssertProblemAsync(locked, HttpStatusCode.UnprocessableEntity, InventoryErrorCodes.DateLocked);
        await TestClient.AssertProblemAsync(mismatch, HttpStatusCode.UnprocessableEntity, InventoryErrorCodes.UnitMismatch);
        Assert.Equal(new QuantityDto(0.25m, "l"), Assert.Single(amountOnly.Batches).Quantity);
    }

    [Fact]
    public async Task Open_WithRule_UsesEarlierDate_WithoutRule_KeepsPackageDate_Twice422()
    {
        await using var app = await InventoryApp.StartAsync(postgres);
        var today = app.Today();
        var noRule = await AddAsync(app, Add(app.Milk.Id, app.Fridge.Id, date: today.AddDays(10)));
        var opened = Assert.Single((await ActAsync(app.Owner, HttpMethod.Post, $"{app.Inventory}/batches/{noRule.Id}/open", ifMatch: noRule.Version)).Batches);
        app.Rules.Set(StorageType.Chilled, Rule);
        var withRule = await AddAsync(app, Add(app.Milk.Id, app.Fridge.Id, date: today.AddDays(10)));

        var ruled = Assert.Single((await ActAsync(app.Owner, HttpMethod.Post, $"{app.Inventory}/batches/{withRule.Id}/open", ifMatch: withRule.Version)).Batches);
        using var again = await SendAsync(app.Owner, HttpMethod.Post, $"{app.Inventory}/batches/{withRule.Id}/open", ifMatch: ruled.Version);

        Assert.Equal((BatchStateDto.Opened, today, today.AddDays(10), ExpiryKindDto.BestBefore), (opened.State, opened.OpenedOn, opened.ExpiresOn, opened.ExpiryKind));
        Assert.Equal((today.AddDays(3), ExpiryKindDto.UseBy, ExpiryStatusDto.Ok), (ruled.ExpiresOn, ruled.ExpiryKind, ruled.Status));
        await TestClient.AssertProblemAsync(again, HttpStatusCode.UnprocessableEntity, InventoryErrorCodes.InvalidState);
    }

    [Fact]
    public async Task Move_IntoFreezerFreezes_OutThaws_BackIsRefused()
    {
        await using var app = await InventoryApp.StartAsync(postgres);
        app.Rules.Set(StorageType.Frozen, Rule).Set(StorageType.Chilled, Rule);
        var today = app.Today();
        var batch = await AddAsync(app, Add(app.Milk.Id, app.Pantry.Id, date: today.AddDays(5)));
        var move = $"{app.Inventory}/batches/{batch.Id}/move";

        var toFridge = Assert.Single((await ActAsync(app.Owner, HttpMethod.Post, move, new MoveBatchRequest(app.Fridge.Id), batch.Version)).Batches);
        var frozen = Assert.Single((await ActAsync(app.Owner, HttpMethod.Post, move, new MoveBatchRequest(app.Freezer.Id), toFridge.Version)).Batches);
        var thawed = Assert.Single((await ActAsync(app.Owner, HttpMethod.Post, move, new MoveBatchRequest(app.Fridge.Id), frozen.Version)).Batches);
        using var refreeze = await SendAsync(app.Owner, HttpMethod.Post, move, new MoveBatchRequest(app.Freezer.Id), thawed.Version);

        Assert.Equal((StorageTypeDto.Chilled, BatchStateDto.Sealed, today.AddDays(5), app.Fridge.Id), (toFridge.StorageType, toFridge.State, toFridge.ExpiresOn, toFridge.StorageLocationId));
        Assert.Equal((BatchStateDto.Frozen, today.AddDays(90), app.Freezer.Id), (frozen.State, frozen.ExpiresOn, frozen.StorageLocationId));
        Assert.Equal((BatchStateDto.Thawed, today.AddDays(1), ExpiryKindDto.UseBy), (thawed.State, thawed.ExpiresOn, thawed.ExpiryKind));
        await TestClient.AssertProblemAsync(refreeze, HttpStatusCode.UnprocessableEntity, InventoryErrorCodes.RefreezeNotAllowed);
    }

    [Fact]
    public async Task Changes_WithoutIfMatch_428_StaleIfMatch_412()
    {
        await using var app = await InventoryApp.StartAsync(postgres);
        var batch = await AddAsync(app, Add(app.Milk.Id, app.Fridge.Id));
        var url = $"{app.Inventory}/batches/{batch.Id}";
        var correct = new CorrectBatchRequest(new QuantityDto(2m, "l"), null, ExpiryKindDto.None);

        using var missing = await SendAsync(app.Owner, HttpMethod.Put, url, correct);
        using var missingOpen = await SendAsync(app.Owner, HttpMethod.Post, $"{url}/open");
        using var missingMove = await SendAsync(app.Owner, HttpMethod.Post, $"{url}/move", new MoveBatchRequest(app.Pantry.Id));
        using var stale = await SendAsync(app.Owner, HttpMethod.Put, url, correct, batch.Version + 1);

        await TestClient.AssertProblemAsync(missing, HttpStatusCode.PreconditionRequired, ErrorCodes.PreconditionRequired);
        await TestClient.AssertProblemAsync(missingOpen, HttpStatusCode.PreconditionRequired, ErrorCodes.PreconditionRequired);
        await TestClient.AssertProblemAsync(missingMove, HttpStatusCode.PreconditionRequired, ErrorCodes.PreconditionRequired);
        await TestClient.AssertProblemAsync(stale, HttpStatusCode.PreconditionFailed, ErrorCodes.PreconditionFailed);
        Assert.Equal($"\"{batch.Version}\"", stale.Headers.ETag!.Tag);
    }

    [Fact]
    public async Task Restricted_CanRead_CannotChange()
    {
        await using var app = await InventoryApp.StartAsync(postgres);
        var batch = await AddAsync(app, Add(app.Milk.Id, app.Fridge.Id));
        using var kid = await app.AddMemberAsync(Contracts.Households.HouseholdRole.Restricted, "kid");
        var url = $"{app.Inventory}/batches/{batch.Id}";

        using var read = await kid.GetAsync(url);
        using var add = await kid.PostAsync($"{app.Inventory}/batches", Add(app.Milk.Id, app.Fridge.Id));
        using var correct = await SendAsync(kid, HttpMethod.Put, url, new CorrectBatchRequest(new QuantityDto(2m, "l"), null, ExpiryKindDto.None), batch.Version);
        using var open = await SendAsync(kid, HttpMethod.Post, $"{url}/open", ifMatch: batch.Version);
        using var move = await SendAsync(kid, HttpMethod.Post, $"{url}/move", new MoveBatchRequest(app.Pantry.Id), batch.Version);
        using var leftover = await kid.PostAsync($"{app.Inventory}/leftovers", new CreateLeftoverRequest(null, app.Milk.Id, app.Fridge.Id, new QuantityDto(1m, "l")));

        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        foreach (var response in new[] { add, correct, open, move, leftover })
        {
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
    }

    [Fact]
    public async Task ForeignHousehold_BatchProductAndLocation_Return404()
    {
        await using var app = await InventoryApp.StartAsync(postgres);
        var otherId = await Scenario.CreateHouseholdAsync(app.Owner, "Other");
        var otherFridge = await Support.HouseholdDataSeed.StorageLocationAsync(app.Services, otherId);
        var otherProduct = await Support.HouseholdDataSeed.ProductAsync(app.Services, otherId, "Bread");
        var foreign = await Support.HouseholdDataSeed.BatchAsync(app.Services, otherId, otherProduct.Id, otherFridge.Id);
        var url = $"{app.Inventory}/batches/{foreign.Id}";

        using var get = await app.Owner.GetAsync(url);
        using var correct = await SendAsync(app.Owner, HttpMethod.Put, url, new CorrectBatchRequest(new QuantityDto(2m, "l"), null, ExpiryKindDto.None), foreign.Version);
        using var open = await SendAsync(app.Owner, HttpMethod.Post, $"{url}/open", ifMatch: foreign.Version);
        using var move = await SendAsync(app.Owner, HttpMethod.Post, $"{url}/move", new MoveBatchRequest(app.Pantry.Id), foreign.Version);
        using var foreignProduct = await app.Owner.PostAsync($"{app.Inventory}/batches", Add(otherProduct.Id, app.Fridge.Id));
        using var foreignLocation = await app.Owner.PostAsync($"{app.Inventory}/batches", Add(app.Milk.Id, otherFridge.Id));
        using var leftover = await app.Owner.PostAsync($"{app.Inventory}/leftovers", new CreateLeftoverRequest(null, otherProduct.Id, app.Fridge.Id, new QuantityDto(1m, "l")));
        var mine = await AddAsync(app, Add(app.Milk.Id, app.Fridge.Id));
        using var moveToForeign = await SendAsync(app.Owner, HttpMethod.Post, $"{app.Inventory}/batches/{mine.Id}/move", new MoveBatchRequest(otherFridge.Id), mine.Version);

        foreach (var response in new[] { get, correct, open, move })
        {
            await TestClient.AssertProblemAsync(response, HttpStatusCode.NotFound, InventoryErrorCodes.BatchNotFound);
        }

        await TestClient.AssertProblemAsync(foreignProduct, HttpStatusCode.NotFound, CatalogErrorCodes.ProductNotFound);
        await TestClient.AssertProblemAsync(foreignLocation, HttpStatusCode.NotFound, CatalogErrorCodes.LocationNotFound);
        await TestClient.AssertProblemAsync(leftover, HttpStatusCode.NotFound, CatalogErrorCodes.ProductNotFound);
        await TestClient.AssertProblemAsync(moveToForeign, HttpStatusCode.NotFound, CatalogErrorCodes.LocationNotFound);
    }
}
