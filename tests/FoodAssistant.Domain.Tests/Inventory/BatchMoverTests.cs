using FoodAssistant.Domain.Expiry;
using FoodAssistant.Domain.Inventory;
using FoodAssistant.Domain.Quantities;
using static FoodAssistant.Domain.Tests.Inventory.TestBatches;

namespace FoodAssistant.Domain.Tests.Inventory;

/// <summary>Moving a batch: the storage type of the target location freezes, thaws or just relocates it.</summary>
public sealed class BatchMoverTests
{
    [Theory]
    [InlineData(StorageType.Chilled)]
    [InlineData(StorageType.Ambient)]
    [InlineData(StorageType.Frozen)]
    public void Move_SameType_ChangesNothing(StorageType type)
    {
        var batch = Sealed(Today.AddDays(5), storage: type);
        var before = StockBatchSnapshot.From(batch);

        var move = BatchMover.Move(batch, type, Today, FullRule);

        Assert.Equal(BatchMove.SameType, move);
        Assert.Equal(before, StockBatchSnapshot.From(batch));
    }

    [Theory]
    [InlineData(StorageType.Chilled)]
    [InlineData(StorageType.Ambient)]
    public void Move_IntoFreezer_Freezes(StorageType from)
    {
        var batch = Sealed(Today.AddDays(5), storage: from);

        var move = BatchMover.Move(batch, StorageType.Frozen, Today, FullRule);

        Assert.Equal(BatchMove.Frozen, move);
        Assert.Equal(BatchState.Frozen, batch.State);
        Assert.Equal(StorageType.Frozen, batch.StorageType);
        Assert.Equal(Today.AddDays(90), batch.ExpiresOn);
    }

    [Theory]
    [InlineData(StorageType.Chilled)]
    [InlineData(StorageType.Ambient)]
    public void Move_OutOfFreezer_Thaws(StorageType target)
    {
        var batch = Sealed(Today.AddDays(5));
        batch.Freeze(Today, FullRule);

        var move = BatchMover.Move(batch, target, Today.AddDays(30), FullRule);

        Assert.Equal(BatchMove.Thawed, move);
        Assert.Equal(BatchState.Thawed, batch.State);
        Assert.Equal(target, batch.StorageType);
        Assert.Equal(Today.AddDays(31), batch.ExpiresOn);
        Assert.Equal(ExpiryKind.UseBy, batch.ExpiryKind);
    }

    [Fact]
    public void Move_BoughtFrozenOutOfFreezer_Thaws()
    {
        var batch = Sealed(Today.AddDays(200), storage: StorageType.Frozen);

        var move = BatchMover.Move(batch, StorageType.Chilled, Today, FullRule);

        Assert.Equal(BatchMove.Thawed, move);
        Assert.Equal(Today.AddDays(1), batch.ExpiresOn);
    }

    [Theory]
    [InlineData(StorageType.Chilled, StorageType.Ambient)]
    [InlineData(StorageType.Ambient, StorageType.Chilled)]
    public void Move_BetweenFridgeAndPantry_KeepsDates(StorageType from, StorageType target)
    {
        var batch = Sealed(Today.AddDays(5), storage: from);
        batch.Open(Today, FullRule);

        var move = BatchMover.Move(batch, target, Today, FullRule);

        Assert.Equal(BatchMove.Relocated, move);
        Assert.Equal(target, batch.StorageType);
        Assert.Equal(BatchState.Opened, batch.State);
        Assert.Equal(Today.AddDays(3), batch.ExpiresOn);
    }

    [Fact]
    public void Move_ThawedIntoFreezer_Throws()
    {
        var batch = Sealed(Today.AddDays(5));
        batch.Freeze(Today, FullRule);
        BatchMover.Move(batch, StorageType.Chilled, Today, FullRule);

        Assert.Throws<InvalidOperationException>(() => BatchMover.Move(batch, StorageType.Frozen, Today, FullRule));
        Assert.Equal(StorageType.Chilled, batch.StorageType);
    }

    [Fact]
    public void Move_NullArguments_Throw()
    {
        var batch = Sealed(null);

        Assert.Throws<ArgumentNullException>(() => BatchMover.Move(null!, StorageType.Chilled, Today, FullRule));
        Assert.Throws<ArgumentNullException>(() => BatchMover.Move(batch, StorageType.Chilled, Today, null!));
    }

    [Fact]
    public void Move_LeftoverToPantry_KeepsPreparedDate()
    {
        var batch = StockBatch.CreateLeftover(Guid.NewGuid(), new Quantity(1m, Unit.Piece), StorageType.Chilled, Today, FullRule);

        BatchMover.Move(batch, StorageType.Ambient, Today, FullRule);

        Assert.Equal(Today, batch.PreparedOn);
        Assert.Equal(Today.AddDays(2), batch.ExpiresOn);
    }
}
