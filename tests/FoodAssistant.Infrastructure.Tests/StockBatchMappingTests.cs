using FoodAssistant.Domain.Expiry;
using FoodAssistant.Domain.Inventory;
using FoodAssistant.Domain.Quantities;
using FoodAssistant.Infrastructure.Inventory;

namespace FoodAssistant.Infrastructure.Tests;

/// <summary>Mapping between the domain batch and its record, without a database.</summary>
public sealed class StockBatchMappingTests
{
    private static readonly DateOnly Today = new(2026, 11, 18);
    private static readonly DateTimeOffset Now = new(2026, 11, 18, 9, 30, 0, TimeSpan.Zero);
    private static readonly ShelfLifeRule Rule = new() { DaysAfterOpening = 3, DaysFrozen = 90, DaysAfterThawing = 1 };

    [Fact]
    public void NewRecord_ThenToDomain_RoundTrips()
    {
        var batch = Sealed();
        batch.Open(Today, Rule);
        var householdId = Guid.CreateVersion7();
        var productId = Guid.CreateVersion7();
        var locationId = Guid.CreateVersion7();

        var record = StockBatchMapping.NewRecord(batch, householdId, productId, locationId, BatchOrigin.Scanned, Today, Now);

        Assert.Equal(StockBatchSnapshot.From(batch), StockBatchSnapshot.From(record.ToDomain()));
        Assert.Equal((householdId, productId, locationId), (record.HouseholdId, record.ProductId, record.StorageLocationId));
        Assert.Equal((BatchOrigin.Scanned, Today), (record.Origin, record.AddedOn));
        Assert.Equal((Now, Now), (record.CreatedAt, record.UpdatedAt));
        Assert.Equal("g", record.QuantityUnitCode);
    }

    [Fact]
    public void Apply_CopiesChangedStateAndStampsUpdatedAt()
    {
        var batch = Sealed();
        var record = StockBatchMapping.NewRecord(batch, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), BatchOrigin.Manual, Today, Now);
        var later = Now.AddHours(2);

        var loaded = record.ToDomain();
        loaded.Freeze(Today, Rule);
        loaded.Consume(new Quantity(100m, Unit.Gram));
        record.Apply(loaded, later);

        Assert.Equal(StockBatchSnapshot.From(loaded), StockBatchSnapshot.From(record.ToDomain()));
        Assert.Equal(BatchState.Frozen, record.State);
        Assert.Equal(400m, record.QuantityValue);
        Assert.Equal(Today.AddDays(5), record.PackageExpiresOn);
        Assert.Equal(Now, record.CreatedAt);
        Assert.Equal(later, record.UpdatedAt);
    }

    [Fact]
    public void Apply_OtherBatch_Throws()
    {
        var record = StockBatchMapping.NewRecord(Sealed(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), BatchOrigin.Manual, Today, Now);

        Assert.Throws<ArgumentException>(() => record.Apply(Sealed(), Now));
    }

    [Fact]
    public void ToDomain_InconsistentRecord_Throws()
    {
        var record = StockBatchMapping.NewRecord(Sealed(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), BatchOrigin.Manual, Today, Now);
        record.ExpiryKind = ExpiryKind.None;

        Assert.Throws<ArgumentException>(record.ToDomain);
    }

    [Fact]
    public void ToDomain_UnknownUnitCode_Throws()
    {
        var record = StockBatchMapping.NewRecord(Sealed(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), BatchOrigin.Manual, Today, Now);
        record.QuantityUnitCode = "cup";

        Assert.Throws<ArgumentException>(record.ToDomain);
    }

    private static StockBatch Sealed() =>
        new(Guid.CreateVersion7(), new Quantity(500m, Unit.Gram), StorageType.Chilled, Today.AddDays(5), ExpiryKind.BestBefore);
}
