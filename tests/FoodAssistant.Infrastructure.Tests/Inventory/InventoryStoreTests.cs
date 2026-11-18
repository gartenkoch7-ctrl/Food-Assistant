using FoodAssistant.Application.Inventory;
using FoodAssistant.Domain.Expiry;
using FoodAssistant.Domain.Inventory;
using FoodAssistant.Domain.Quantities;
using FoodAssistant.Infrastructure.Inventory;
using Microsoft.EntityFrameworkCore;

namespace FoodAssistant.Infrastructure.Tests.Inventory;

/// <summary>Saving batches: deleting emptied ones, re-creating with the same id, versions, household filter.</summary>
public sealed class InventoryStoreTests(PostgresFixture postgres)
{
    private static readonly DateTimeOffset Now = new(2026, 11, 18, 9, 0, 0, TimeSpan.Zero);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Save_DeletesEmptiedBatch_UndoRecreatesItWithTheSameId()
    {
        await using var database = await TestDatabase.CreateAsync(postgres.NewDatabaseConnectionString());
        var (householdId, _, batch) = await SeedAsync(database);
        BatchContext loaded;

        await using (var scope = database.CreateScope(householdId))
        {
            var store = scope.Get<IInventoryStore>();
            loaded = (await store.GetBatchAsync(batch.Id, Ct))!;
            loaded.Batch.Consume(loaded.Batch.Quantity);
            var result = await store.SaveAsync(BatchChange.Of(Now, removed: [new BatchRemoval(batch.Id, loaded.Version)]), Ct);

            Assert.Equal(BatchSaveStatus.Saved, result.Status);
            Assert.False(await scope.Db.Set<StockBatchRecord>().AnyAsync(Ct));
        }

        await using (var scope = database.CreateScope(householdId))
        {
            var restored = StockBatch.Restore(StockBatchSnapshot.From(batch.ToDomain()));
            var result = await scope.Get<IInventoryStore>().SaveAsync(
                BatchChange.Of(Now, added: [new NewBatch(restored, loaded.ProductId, loaded.StorageLocationId, loaded.Origin, loaded.AddedOn)]),
                Ct);

            var record = await scope.Db.Set<StockBatchRecord>().AsNoTracking().SingleAsync(Ct);
            Assert.Equal(BatchSaveStatus.Saved, result.Status);
            Assert.Equal((batch.Id, 1m, loaded.AddedOn, Now), (record.Id, record.QuantityValue, record.AddedOn, record.UpdatedAt));
            Assert.Equal(record.Version, (uint)result.Versions[batch.Id]);
        }
    }

    [Fact]
    public async Task Save_StaleVersion_IsConflict_ExistingId_IsDuplicate()
    {
        await using var database = await TestDatabase.CreateAsync(postgres.NewDatabaseConnectionString());
        var (householdId, _, batch) = await SeedAsync(database);
        await using var first = database.CreateScope(householdId);
        await using var second = database.CreateScope(householdId);
        var stale = (await first.Get<IInventoryStore>().GetBatchAsync(batch.Id, Ct))!;
        var fresh = (await second.Get<IInventoryStore>().GetBatchAsync(batch.Id, Ct))!;
        fresh.Batch.Consume(new Quantity(0.5m, Unit.Litre));
        await second.Get<IInventoryStore>().SaveAsync(
            BatchChange.Of(Now, updated: [new BatchUpdate(fresh.Batch, fresh.StorageLocationId, fresh.Version)]), Ct);

        stale.Batch.Consume(new Quantity(0.25m, Unit.Litre));
        var conflict = await first.Get<IInventoryStore>().SaveAsync(
            BatchChange.Of(Now, updated: [new BatchUpdate(stale.Batch, stale.StorageLocationId, stale.Version)]), Ct);
        var copy = new StockBatch(batch.Id, new Quantity(1m, Unit.Litre), StorageType.Chilled, null, ExpiryKind.None);
        var duplicate = await first.Get<IInventoryStore>().SaveAsync(
            BatchChange.Of(Now, added: [new NewBatch(copy, stale.ProductId, stale.StorageLocationId, BatchOrigin.Manual, stale.AddedOn)]), Ct);

        Assert.Equal(BatchSaveStatus.Conflict, conflict.Status);
        Assert.Equal(BatchSaveStatus.Duplicate, duplicate.Status);
        Assert.Equal(0.5m, (await second.Db.Set<StockBatchRecord>().AsNoTracking().SingleAsync(Ct)).QuantityValue);
    }

    [Fact]
    public async Task Reads_SeeOnlyTheActiveHousehold()
    {
        await using var database = await TestDatabase.CreateAsync(postgres.NewDatabaseConnectionString());
        var (otherHousehold, productId, batch) = await SeedAsync(database);
        var (ownHousehold, _, _) = await SeedAsync(database, "second");
        await using var scope = database.CreateScope(ownHousehold);
        var store = scope.Get<IInventoryStore>();

        Assert.Null(await store.GetBatchAsync(batch.Id, Ct));
        Assert.Empty(await store.GetBatchesOfProductAsync(productId, Ct));
        Assert.Null(await store.GetProductAsync(productId, Ct));
        Assert.Null(await store.GetLocationAsync(batch.StorageLocationId, Ct));
        Assert.DoesNotContain(await store.ListAsync(Ct), p => p.Product.Id == productId);
        Assert.NotEqual(otherHousehold, ownHousehold);
    }

    private static async Task<(Guid HouseholdId, Guid ProductId, StockBatchRecord Batch)> SeedAsync(
        TestDatabase database,
        string userName = "owner")
    {
        Guid householdId;
        await using (var scope = database.CreateScope())
        {
            var user = await Seed.UserAsync(scope.Db, userName);
            householdId = (await Seed.HouseholdAsync(scope.Db, user.Id)).Id;
        }

        await using var seedScope = database.CreateScope(householdId);
        var location = await M1Seed.StorageLocationAsync(seedScope.Db, householdId);
        var product = await M1Seed.ProductAsync(seedScope.Db, householdId);
        var batch = await M1Seed.BatchAsync(seedScope.Db, householdId, product.Id, location.Id);
        return (householdId, product.Id, batch);
    }
}
