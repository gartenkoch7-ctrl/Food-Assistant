using FoodAssistant.Domain.Checkout;
using FoodAssistant.Domain.Expiry;
using FoodAssistant.Domain.Inventory;
using FoodAssistant.Domain.Quantities;

namespace FoodAssistant.Domain.Tests.Checkout;

public sealed class PurchasedBatchTests
{
    private static readonly DateOnly Today = new(2026, 11, 18);
    private static readonly ShelfLifeRule FrozenRule = new() { DaysFrozen = 90 };
    private static readonly Quantity OneLitre = new(1m, Unit.Litre);

    [Theory]
    [InlineData(StorageType.Chilled)]
    [InlineData(StorageType.Ambient)]
    public void Create_NotFrozen_SealedWithPackageDate(StorageType type)
    {
        var batch = Create(type, Today.AddDays(5), ExpiryKind.BestBefore, selfFrozen: true);

        Assert.Equal((type, BatchState.Sealed, Today.AddDays(5), ExpiryKind.BestBefore), (batch.StorageType, batch.State, batch.ExpiresOn, batch.ExpiryKind));
    }

    [Fact]
    public void Create_FreezerWithDate_IsBoughtFrozenFood()
    {
        var batch = Create(StorageType.Frozen, Today.AddDays(200), ExpiryKind.BestBefore, selfFrozen: false);

        Assert.Equal((StorageType.Frozen, BatchState.Sealed, Today.AddDays(200)), (batch.StorageType, batch.State, batch.ExpiresOn));
        Assert.Null(batch.FrozenOn);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Create_FreezerWithoutDateOrSelfFrozen_FrozenTodayByRule(bool withDate)
    {
        DateOnly? date = withDate ? Today.AddDays(4) : null;

        var batch = Create(StorageType.Frozen, date, withDate ? ExpiryKind.UseBy : ExpiryKind.None, selfFrozen: withDate);

        Assert.Equal((BatchState.Frozen, Today, Today.AddDays(90), ExpiryKind.UseBy), (batch.State, batch.FrozenOn, batch.ExpiresOn, batch.ExpiryKind));
        Assert.Equal(date, batch.PackageExpiresOn);
    }

    [Fact]
    public void Create_FreezerWithoutRule_NoDate()
    {
        var batch = PurchasedBatch.Create(Guid.NewGuid(), OneLitre, StorageType.Frozen, null, ExpiryKind.None, false, ShelfLifeRule.Empty, Today);

        Assert.Equal((BatchState.Frozen, (DateOnly?)null, ExpiryKind.None), (batch.State, batch.ExpiresOn, batch.ExpiryKind));
    }

    [Fact]
    public void ShelfLifeOf_SealedWithPackageDate_DaysAndKind() =>
        Assert.Equal(
            new LastPurchase(6, ExpiryKind.UseBy),
            PurchasedBatch.ShelfLifeOf(Create(StorageType.Chilled, Today.AddDays(6), ExpiryKind.UseBy), Today));

    [Fact]
    public void ShelfLifeOf_FrozenAtHome_IsNull() =>
        Assert.Null(PurchasedBatch.ShelfLifeOf(Create(StorageType.Frozen, Today.AddDays(6), ExpiryKind.UseBy, selfFrozen: true), Today));

    [Fact]
    public void ShelfLifeOf_WithoutDate_IsNull() =>
        Assert.Null(PurchasedBatch.ShelfLifeOf(Create(StorageType.Chilled, null, ExpiryKind.None), Today));

    [Fact]
    public void ShelfLifeOf_DateInThePast_IsNull() =>
        Assert.Null(PurchasedBatch.ShelfLifeOf(Create(StorageType.Chilled, Today.AddDays(-1), ExpiryKind.BestBefore), Today));

    [Fact]
    public void ShelfLifeOf_DateToday_IsZeroDays() =>
        Assert.Equal(0, PurchasedBatch.ShelfLifeOf(Create(StorageType.Ambient, Today, ExpiryKind.BestBefore), Today)!.Days);

    private static StockBatch Create(StorageType type, DateOnly? date, ExpiryKind kind, bool selfFrozen = false) =>
        PurchasedBatch.Create(Guid.NewGuid(), OneLitre, type, date, kind, selfFrozen, FrozenRule, Today);
}
