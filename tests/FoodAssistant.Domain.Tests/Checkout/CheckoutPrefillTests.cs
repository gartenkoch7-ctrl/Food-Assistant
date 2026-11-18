using FoodAssistant.Domain.Checkout;
using FoodAssistant.Domain.Expiry;
using FoodAssistant.Domain.Inventory;

namespace FoodAssistant.Domain.Tests.Checkout;

public sealed class CheckoutPrefillTests
{
    private static readonly DateOnly Today = new(2026, 11, 18);
    private static readonly ShelfLifeRule FrozenRule = new() { DaysFrozen = 90 };
    private static readonly LocationOption Pantry = new(Guid.NewGuid(), StorageType.Ambient);
    private static readonly LocationOption Fridge = new(Guid.NewGuid(), StorageType.Chilled);
    private static readonly LocationOption Freezer = new(Guid.NewGuid(), StorageType.Frozen);
    private static readonly LocationOption[] Locations = [Pantry, Fridge, Freezer];

    [Fact]
    public void ProposeLocation_ProductDefault_Wins() =>
        Assert.Equal(Freezer, CheckoutPrefill.ProposeLocation(Freezer.Id, Fridge.Id, StorageType.Ambient, Locations));

    [Fact]
    public void ProposeLocation_NoDefault_TakesLastBatchLocation() =>
        Assert.Equal(Freezer, CheckoutPrefill.ProposeLocation(null, Freezer.Id, StorageType.Chilled, Locations));

    [Fact]
    public void ProposeLocation_UnknownIds_FallBackToCategoryType() =>
        Assert.Equal(Fridge, CheckoutPrefill.ProposeLocation(Guid.NewGuid(), Guid.NewGuid(), StorageType.Chilled, Locations));

    [Fact]
    public void ProposeLocation_NoLocationOfCategoryType_TakesFirst() =>
        Assert.Equal(Pantry, CheckoutPrefill.ProposeLocation(null, null, StorageType.Frozen, [Pantry, Fridge]));

    [Fact]
    public void ProposeLocation_FreeText_TakesFirst() =>
        Assert.Equal(Pantry, CheckoutPrefill.ProposeLocation(null, null, null, Locations));

    [Fact]
    public void ProposeLocation_NoLocations_IsNull() =>
        Assert.Null(CheckoutPrefill.ProposeLocation(Fridge.Id, null, StorageType.Chilled, []));

    [Theory]
    [InlineData(StorageType.Chilled)]
    [InlineData(StorageType.Frozen)]
    [InlineData(null)]
    public void Propose_LastPurchaseKnown_TodayPlusDaysWithItsKind(StorageType? target)
    {
        var proposal = CheckoutPrefill.Propose(target, new LastPurchase(7, ExpiryKind.BestBefore), FrozenRule, Today);

        Assert.Equal(new ExpiryProposal(Today.AddDays(7), ExpiryKind.BestBefore, false), proposal);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(PurchasedBatch.MaxRememberedDays + 1)]
    [InlineData(int.MaxValue)]
    public void Propose_LastPurchaseOutOfRange_IsNoSuggestion(int days) =>
        Assert.Equal(ExpiryProposal.None, CheckoutPrefill.Propose(StorageType.Chilled, new LastPurchase(days, ExpiryKind.BestBefore), FrozenRule, Today));

    [Fact]
    public void Propose_FreezerWithoutLastPurchase_RuleFromTodayAsUseBy() =>
        Assert.Equal(
            new ExpiryProposal(Today.AddDays(90), ExpiryKind.UseBy, true),
            CheckoutPrefill.Propose(StorageType.Frozen, null, FrozenRule, Today));

    [Fact]
    public void Propose_FreezerWithoutRule_NoDate() =>
        Assert.Equal(ExpiryProposal.None, CheckoutPrefill.Propose(StorageType.Frozen, null, ShelfLifeRule.Empty, Today));

    [Fact]
    public void Propose_LastPurchaseWithoutKind_IsIgnored() =>
        Assert.Equal(
            new ExpiryProposal(Today.AddDays(90), ExpiryKind.UseBy, true),
            CheckoutPrefill.Propose(StorageType.Frozen, new LastPurchase(5, ExpiryKind.None), FrozenRule, Today));

    [Theory]
    [InlineData(StorageType.Chilled)]
    [InlineData(StorageType.Ambient)]
    [InlineData(null)]
    public void Propose_NotFrozenWithoutLastPurchase_NoDate(StorageType? target) =>
        Assert.Equal(ExpiryProposal.None, CheckoutPrefill.Propose(target, null, FrozenRule, Today));
}
