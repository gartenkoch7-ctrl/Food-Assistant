using FoodAssistant.Domain.Expiry;
using FoodAssistant.Domain.Inventory;
using FoodAssistant.Domain.Quantities;
using static FoodAssistant.Domain.Tests.Inventory.TestBatches;

namespace FoodAssistant.Domain.Tests.Inventory;

/// <summary>Persistence round trip via <see cref="StockBatchSnapshot"/> and <see cref="StockBatch.Restore"/>.</summary>
public sealed class StockBatchSnapshotTests
{
    public static TheoryData<string> States => ["sealed", "opened", "frozen", "thawed", "leftover", "used_up"];

    [Theory]
    [MemberData(nameof(States))]
    public void Restore_FromSnapshot_RoundTripsEveryState(string state)
    {
        var batch = BatchIn(state);
        var snapshot = StockBatchSnapshot.From(batch);

        var restored = StockBatch.Restore(snapshot);

        Assert.Equal(snapshot, StockBatchSnapshot.From(restored));
    }

    [Fact]
    public void Restore_KeepsBehaviour_RestoredFrozenBatchCanBeThawed()
    {
        var batch = Sealed(Today.AddDays(5));
        batch.Freeze(Today, FullRule);

        var restored = StockBatch.Restore(StockBatchSnapshot.From(batch));
        restored.Thaw(Today.AddDays(1), FullRule);

        Assert.Equal(BatchState.Thawed, restored.State);
        Assert.Equal(Today.AddDays(2), restored.ExpiresOn);
        Assert.Equal(Today.AddDays(5), restored.PackageExpiresOn);
    }

    [Fact]
    public void Validate_EmptyId_Throws() =>
        Assert.Throws<ArgumentException>(() => StockBatch.Restore(Valid() with { Id = Guid.Empty }));

    [Fact]
    public void Validate_QuantityWithoutUnit_Throws() =>
        Assert.Throws<ArgumentException>(() => StockBatch.Restore(Valid() with { Quantity = default }));

    [Theory]
    [InlineData(true, ExpiryKind.None)]
    [InlineData(false, ExpiryKind.BestBefore)]
    public void Validate_PackageDateAndKindMismatch_Throws(bool withDate, ExpiryKind kind) =>
        Assert.Throws<ArgumentException>(() => StockBatch.Restore(Valid() with
        {
            PackageExpiresOn = withDate ? Today : null,
            PackageExpiryKind = kind,
        }));

    [Theory]
    [InlineData(true, ExpiryKind.None)]
    [InlineData(false, ExpiryKind.UseBy)]
    public void Validate_ExpiryDateAndKindMismatch_Throws(bool withDate, ExpiryKind kind) =>
        Assert.Throws<ArgumentException>(() => StockBatch.Restore(Valid() with
        {
            ExpiresOn = withDate ? Today : null,
            ExpiryKind = kind,
        }));

    [Fact]
    public void Restore_Null_Throws() =>
        Assert.Throws<ArgumentNullException>(() => StockBatch.Restore(null!));

    private static StockBatchSnapshot Valid() => StockBatchSnapshot.From(Sealed(Today.AddDays(3)));

    private static StockBatch BatchIn(string state)
    {
        var batch = Sealed(Today.AddDays(10), ExpiryKind.BestBefore, 500m, Unit.Gram);
        switch (state)
        {
            case "opened":
                batch.Open(Today, FullRule);
                break;
            case "frozen":
                batch.Freeze(Today, FullRule);
                break;
            case "thawed":
                batch.Freeze(Today, FullRule);
                batch.Thaw(Today.AddDays(30), FullRule);
                break;
            case "leftover":
                return StockBatch.CreateLeftover(Guid.NewGuid(), new Quantity(2m, Unit.Piece), StorageType.Chilled, Today, FullRule);
            case "used_up":
                batch.Consume(new Quantity(0.5m, Unit.Kilogram));
                break;
        }

        return batch;
    }
}
