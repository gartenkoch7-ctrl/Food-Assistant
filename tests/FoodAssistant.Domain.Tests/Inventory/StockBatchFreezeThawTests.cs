using FoodAssistant.Domain.Expiry;
using FoodAssistant.Domain.Inventory;
using FoodAssistant.Domain.Quantities;
using static FoodAssistant.Domain.Tests.Inventory.TestBatches;

namespace FoodAssistant.Domain.Tests.Inventory;

/// <summary>States "eingefroren" (package date rests) and "aufgetaut" (short rule from thawing).</summary>
public sealed class StockBatchFreezeThawTests
{
    [Fact]
    public void Freeze_Sealed_UsesFrozenRuleAndPackageDateRests()
    {
        var batch = Sealed(Today.AddDays(2), ExpiryKind.UseBy);

        batch.Freeze(Today, FullRule);

        Assert.Equal(BatchState.Frozen, batch.State);
        Assert.Equal(StorageType.Frozen, batch.StorageType);
        Assert.Equal(Today, batch.FrozenOn);
        Assert.Equal(Today.AddDays(90), batch.ExpiresOn);
        Assert.Equal(ExpiryKind.UseBy, batch.ExpiryKind);
        Assert.Equal(Today.AddDays(2), batch.PackageExpiresOn);
        Assert.Equal(ExpiryKind.UseBy, batch.PackageExpiryKind);
    }

    [Fact]
    public void Freeze_Opened_KeepsOpeningDate()
    {
        var batch = Sealed(Today.AddDays(10));
        batch.Open(Today, FullRule);

        batch.Freeze(Today.AddDays(1), FullRule);

        Assert.Equal(Today, batch.OpenedOn);
        Assert.Equal(Today.AddDays(91), batch.ExpiresOn);
    }

    [Fact]
    public void Freeze_Leftover_UsesFrozenRule()
    {
        var batch = StockBatch.CreateLeftover(
            Guid.NewGuid(), new Quantity(1m, Unit.Piece), StorageType.Chilled, Today, FullRule);

        batch.Freeze(Today, FullRule);

        Assert.Equal(BatchState.Frozen, batch.State);
        Assert.Equal(Today, batch.PreparedOn);
        Assert.Equal(Today.AddDays(90), batch.ExpiresOn);
    }

    [Fact]
    public void Freeze_NoFrozenRule_HasNoDate()
    {
        var batch = Sealed(Today.AddDays(5));

        batch.Freeze(Today, ShelfLifeRule.Empty);

        Assert.Null(batch.ExpiresOn);
        Assert.Equal(ExpiryKind.None, batch.ExpiryKind);
        Assert.Equal(Today.AddDays(5), batch.PackageExpiresOn);
    }

    [Fact]
    public void Freeze_AlreadyInFreezer_Throws()
    {
        var batch = Sealed(Today.AddDays(200), storage: StorageType.Frozen);

        Assert.Throws<InvalidOperationException>(() => batch.Freeze(Today, FullRule));
    }

    [Fact]
    public void Freeze_Thawed_ThrowsBecauseRefreezingIsUnsafe()
    {
        var batch = Sealed(Today.AddDays(5));
        batch.Freeze(Today, FullRule);
        batch.Thaw(Today.AddDays(10), FullRule);

        Assert.Throws<InvalidOperationException>(() => batch.Freeze(Today.AddDays(10), FullRule));
    }

    [Fact]
    public void Freeze_BeforeOpening_Throws()
    {
        var batch = Sealed(Today.AddDays(10));
        batch.Open(Today, FullRule);

        Assert.Throws<ArgumentOutOfRangeException>(() => batch.Freeze(Today.AddDays(-1), FullRule));
    }

    [Fact]
    public void Freeze_NullRule_Throws() =>
        Assert.Throws<ArgumentNullException>(() => Sealed(Today).Freeze(Today, null!));

    [Theory]
    [InlineData(StorageType.Chilled)]
    [InlineData(StorageType.Ambient)]
    public void Thaw_Frozen_UsesThawRuleFromThawingDay(StorageType target)
    {
        var batch = Sealed(Today.AddDays(5), ExpiryKind.BestBefore);
        batch.Freeze(Today, FullRule);
        var thawDay = Today.AddDays(40);

        batch.Thaw(thawDay, FullRule, target);

        Assert.Equal(BatchState.Thawed, batch.State);
        Assert.Equal(target, batch.StorageType);
        Assert.Equal(thawDay, batch.ThawedOn);
        Assert.Equal(thawDay.AddDays(1), batch.ExpiresOn);
        Assert.Equal(ExpiryKind.UseBy, batch.ExpiryKind);
        Assert.Equal(Today.AddDays(5), batch.PackageExpiresOn);
    }

    [Fact]
    public void Thaw_DefaultTarget_IsChilled()
    {
        var batch = Sealed(Today.AddDays(5));
        batch.Freeze(Today, FullRule);

        batch.Thaw(Today, FullRule);

        Assert.Equal(StorageType.Chilled, batch.StorageType);
    }

    [Fact]
    public void Thaw_BoughtFrozenGoods_UsesThawRule()
    {
        var batch = Sealed(Today.AddDays(300), storage: StorageType.Frozen);

        batch.Thaw(Today, FullRule);

        Assert.Equal(BatchState.Thawed, batch.State);
        Assert.Equal(Today.AddDays(1), batch.ExpiresOn);
    }

    [Fact]
    public void Thaw_LeftoverStoredInFreezer_UsesThawRule()
    {
        var batch = StockBatch.CreateLeftover(
            Guid.NewGuid(), new Quantity(1m, Unit.Piece), StorageType.Frozen, Today, FullRule);

        batch.Thaw(Today.AddDays(20), FullRule);

        Assert.Equal(BatchState.Thawed, batch.State);
        Assert.Equal(Today.AddDays(21), batch.ExpiresOn);
    }

    [Fact]
    public void Thaw_NoThawRule_HasNoDate()
    {
        var batch = Sealed(Today.AddDays(5));
        batch.Freeze(Today, FullRule);

        batch.Thaw(Today, ShelfLifeRule.Empty);

        Assert.Null(batch.ExpiresOn);
        Assert.Equal(ExpiryKind.None, batch.ExpiryKind);
    }

    [Fact]
    public void Thaw_NotInFreezer_Throws() =>
        Assert.Throws<InvalidOperationException>(() => Sealed(Today).Thaw(Today, FullRule));

    [Fact]
    public void Thaw_TargetFrozen_Throws()
    {
        var batch = Sealed(Today.AddDays(5));
        batch.Freeze(Today, FullRule);

        Assert.Throws<ArgumentException>(() => batch.Thaw(Today, FullRule, StorageType.Frozen));
    }

    [Fact]
    public void Thaw_BeforeFreezing_Throws()
    {
        var batch = Sealed(Today.AddDays(5));
        batch.Freeze(Today, FullRule);

        Assert.Throws<ArgumentOutOfRangeException>(() => batch.Thaw(Today.AddDays(-1), FullRule));
    }

    [Fact]
    public void Thaw_NullRule_Throws() =>
        Assert.Throws<ArgumentNullException>(() => Sealed(Today, storage: StorageType.Frozen).Thaw(Today, null!));
}
