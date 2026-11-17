using FoodAssistant.Domain.Expiry;
using FoodAssistant.Domain.Inventory;
using FoodAssistant.Domain.Quantities;
using static FoodAssistant.Domain.Tests.Inventory.TestBatches;

namespace FoodAssistant.Domain.Tests.Inventory;

public sealed class StockBatchCreationTests
{
    [Theory]
    [InlineData(ExpiryKind.BestBefore)]
    [InlineData(ExpiryKind.UseBy)]
    public void Constructor_PackageDate_IsSealedWithPackageDate(ExpiryKind kind)
    {
        var id = Guid.NewGuid();
        var date = Today.AddDays(10);

        var batch = new StockBatch(id, new Quantity(500m, Unit.Gram), StorageType.Ambient, date, kind);

        Assert.Equal(id, batch.Id);
        Assert.Equal(BatchState.Sealed, batch.State);
        Assert.Equal(StorageType.Ambient, batch.StorageType);
        Assert.Equal(new Quantity(500m, Unit.Gram), batch.Quantity);
        Assert.Equal(date, batch.PackageExpiresOn);
        Assert.Equal(date, batch.ExpiresOn);
        Assert.Equal(kind, batch.PackageExpiryKind);
        Assert.Equal(kind, batch.ExpiryKind);
        Assert.False(batch.IsEmpty);
        Assert.Null(batch.OpenedOn);
        Assert.Null(batch.FrozenOn);
        Assert.Null(batch.ThawedOn);
        Assert.Null(batch.PreparedOn);
    }

    [Fact]
    public void Constructor_NoDate_IsWithoutDate()
    {
        var batch = Sealed(null);

        Assert.Null(batch.ExpiresOn);
        Assert.Equal(ExpiryKind.None, batch.ExpiryKind);
    }

    [Fact]
    public void Constructor_DateWithoutKind_Throws() =>
        Assert.Throws<ArgumentException>(() => new StockBatch(
            Guid.NewGuid(), new Quantity(1m, Unit.Piece), StorageType.Chilled, Today, ExpiryKind.None));

    [Fact]
    public void Constructor_KindWithoutDate_Throws() =>
        Assert.Throws<ArgumentException>(() => new StockBatch(
            Guid.NewGuid(), new Quantity(1m, Unit.Piece), StorageType.Chilled, null, ExpiryKind.UseBy));

    [Fact]
    public void Constructor_EmptyId_Throws() =>
        Assert.Throws<ArgumentException>(() => new StockBatch(
            Guid.Empty, new Quantity(1m, Unit.Piece), StorageType.Chilled, null, ExpiryKind.None));

    [Fact]
    public void Constructor_ZeroQuantity_Throws() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new StockBatch(
            Guid.NewGuid(), Quantity.Zero(Unit.Gram), StorageType.Chilled, null, ExpiryKind.None));

    [Fact]
    public void Constructor_DefaultQuantity_Throws() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new StockBatch(
            Guid.NewGuid(), default, StorageType.Chilled, null, ExpiryKind.None));

    [Fact]
    public void CreateLeftover_RuleWithLeftoverDays_DatesFromPreparation()
    {
        var batch = StockBatch.CreateLeftover(
            Guid.NewGuid(), new Quantity(2m, Unit.Piece), StorageType.Chilled, Today, FullRule);

        Assert.Equal(BatchState.Leftover, batch.State);
        Assert.Equal(Today, batch.PreparedOn);
        Assert.Equal(Today.AddDays(2), batch.ExpiresOn);
        Assert.Equal(ExpiryKind.UseBy, batch.ExpiryKind);
        Assert.Null(batch.PackageExpiresOn);
        Assert.Equal(ExpiryKind.None, batch.PackageExpiryKind);
    }

    [Fact]
    public void CreateLeftover_NoLeftoverRule_HasNoDate()
    {
        var batch = StockBatch.CreateLeftover(
            Guid.NewGuid(), new Quantity(1m, Unit.Piece), StorageType.Chilled, Today, ShelfLifeRule.Empty);

        Assert.Null(batch.ExpiresOn);
        Assert.Equal(ExpiryKind.None, batch.ExpiryKind);
    }

    [Fact]
    public void CreateLeftover_NullRule_Throws() =>
        Assert.Throws<ArgumentNullException>(() => StockBatch.CreateLeftover(
            Guid.NewGuid(), new Quantity(1m, Unit.Piece), StorageType.Chilled, Today, null!));

    [Fact]
    public void Consume_Part_ChangesOnlyQuantity()
    {
        var batch = Sealed(Today.AddDays(5), amount: 1m, unit: Unit.Kilogram);
        batch.Open(Today, FullRule);
        var (state, expiresOn, kind) = (batch.State, batch.ExpiresOn, batch.ExpiryKind);

        batch.Consume(new Quantity(300m, Unit.Gram));

        Assert.Equal(new Quantity(0.7m, Unit.Kilogram), batch.Quantity);
        Assert.Equal(state, batch.State);
        Assert.Equal(expiresOn, batch.ExpiresOn);
        Assert.Equal(kind, batch.ExpiryKind);
        Assert.False(batch.IsEmpty);
    }

    [Fact]
    public void Consume_All_LeavesEmptyBatch()
    {
        var batch = Sealed(Today, amount: 2m);

        batch.Consume(new Quantity(2m, Unit.Piece));

        Assert.True(batch.IsEmpty);
        Assert.Equal(BatchState.Sealed, batch.State);
    }

    [Fact]
    public void Consume_MoreThanAvailable_ThrowsAndKeepsQuantity()
    {
        var batch = Sealed(Today, amount: 2m);

        Assert.Throws<InvalidOperationException>(() => batch.Consume(new Quantity(3m, Unit.Piece)));
        Assert.Equal(new Quantity(2m, Unit.Piece), batch.Quantity);
    }

    [Fact]
    public void Consume_IncompatibleUnit_Throws() =>
        Assert.Throws<InvalidOperationException>(() => Sealed(Today).Consume(new Quantity(1m, Unit.Gram)));

    [Fact]
    public void Consume_Zero_Throws() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => Sealed(Today).Consume(Quantity.Zero(Unit.Piece)));
}
