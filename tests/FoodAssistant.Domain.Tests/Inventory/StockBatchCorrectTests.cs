using FoodAssistant.Domain.Expiry;
using FoodAssistant.Domain.Inventory;
using FoodAssistant.Domain.Quantities;
using static FoodAssistant.Domain.Tests.Inventory.TestBatches;

namespace FoodAssistant.Domain.Tests.Inventory;

/// <summary>Correcting typing errors (F14): amount always, date and kind only while sealed.</summary>
public sealed class StockBatchCorrectTests
{
    [Fact]
    public void Correct_Sealed_ChangesAmountDateAndKind()
    {
        var batch = Sealed(Today.AddDays(5), ExpiryKind.BestBefore, amount: 1m, unit: Unit.Kilogram);

        batch.Correct(new Quantity(800m, Unit.Gram), Today.AddDays(3), ExpiryKind.UseBy);

        Assert.Equal(new Quantity(800m, Unit.Gram), batch.Quantity);
        Assert.Equal(Today.AddDays(3), batch.PackageExpiresOn);
        Assert.Equal(ExpiryKind.UseBy, batch.PackageExpiryKind);
        Assert.Equal(Today.AddDays(3), batch.ExpiresOn);
        Assert.Equal(ExpiryKind.UseBy, batch.ExpiryKind);
    }

    [Fact]
    public void Correct_Sealed_CanRemoveTheDate()
    {
        var batch = Sealed(Today.AddDays(5));

        batch.Correct(new Quantity(2m, Unit.Piece), null, ExpiryKind.None);

        Assert.Null(batch.PackageExpiresOn);
        Assert.Null(batch.ExpiresOn);
        Assert.Equal(ExpiryKind.None, batch.ExpiryKind);
    }

    [Fact]
    public void Correct_Opened_SameDate_ChangesOnlyAmount()
    {
        var batch = Sealed(Today.AddDays(10), ExpiryKind.BestBefore, amount: 3m);
        batch.Open(Today, FullRule);

        batch.Correct(new Quantity(2m, Unit.Piece), Today.AddDays(10), ExpiryKind.BestBefore);

        Assert.Equal(new Quantity(2m, Unit.Piece), batch.Quantity);
        Assert.Equal(Today.AddDays(3), batch.ExpiresOn);
        Assert.Equal(ExpiryKind.UseBy, batch.ExpiryKind);
        Assert.Equal(Today.AddDays(10), batch.PackageExpiresOn);
    }

    [Theory]
    [InlineData(11, ExpiryKind.BestBefore)]
    [InlineData(10, ExpiryKind.UseBy)]
    public void Correct_Opened_DifferentDateOrKind_Throws(int days, ExpiryKind kind)
    {
        var batch = Sealed(Today.AddDays(10), ExpiryKind.BestBefore);
        batch.Open(Today, FullRule);

        Assert.Throws<InvalidOperationException>(
            () => batch.Correct(new Quantity(1m, Unit.Piece), Today.AddDays(days), kind));
        Assert.Equal(new Quantity(1m, Unit.Piece), batch.Quantity);
    }

    [Fact]
    public void Correct_Frozen_DifferentDate_Throws()
    {
        var batch = Sealed(Today.AddDays(10));
        batch.Freeze(Today, FullRule);

        Assert.Throws<InvalidOperationException>(
            () => batch.Correct(new Quantity(1m, Unit.Piece), Today.AddDays(20), ExpiryKind.BestBefore));
    }

    [Fact]
    public void Correct_Leftover_KeepsRuleDate()
    {
        var batch = StockBatch.CreateLeftover(Guid.NewGuid(), new Quantity(1m, Unit.Piece), StorageType.Chilled, Today, FullRule);

        batch.Correct(new Quantity(2m, Unit.Piece), null, ExpiryKind.None);

        Assert.Equal(new Quantity(2m, Unit.Piece), batch.Quantity);
        Assert.Equal(Today.AddDays(2), batch.ExpiresOn);
    }

    [Fact]
    public void Correct_ZeroAmount_Throws()
    {
        var batch = Sealed(null);

        Assert.Throws<ArgumentException>(() => batch.Correct(Quantity.Zero(Unit.Piece), null, ExpiryKind.None));
    }

    [Fact]
    public void Correct_DefaultAmount_Throws()
    {
        var batch = Sealed(null);

        Assert.Throws<ArgumentException>(() => batch.Correct(default, null, ExpiryKind.None));
    }

    [Fact]
    public void Correct_OtherDimension_Throws()
    {
        var batch = Sealed(null, amount: 1m, unit: Unit.Kilogram);

        Assert.Throws<ArgumentException>(() => batch.Correct(new Quantity(1m, Unit.Litre), null, ExpiryKind.None));
    }

    [Theory]
    [InlineData(true, ExpiryKind.None)]
    [InlineData(false, ExpiryKind.BestBefore)]
    public void Correct_DateAndKindMismatch_Throws(bool withDate, ExpiryKind kind)
    {
        var batch = Sealed(Today.AddDays(5));
        DateOnly? date = withDate ? Today.AddDays(5) : null;

        Assert.Throws<ArgumentException>(() => batch.Correct(new Quantity(1m, Unit.Piece), date, kind));
    }
}
