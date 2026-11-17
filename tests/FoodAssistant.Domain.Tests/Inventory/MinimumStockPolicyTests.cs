using FoodAssistant.Domain.Inventory;
using FoodAssistant.Domain.Quantities;
using static FoodAssistant.Domain.Tests.Inventory.TestBatches;

namespace FoodAssistant.Domain.Tests.Inventory;

public sealed class MinimumStockPolicyTests
{
    [Fact]
    public void TotalStock_MixedConvertibleUnits_SumsInTargetUnit()
    {
        StockBatch[] batches = [SealedInDays(1, 1m, Unit.Litre), SealedInDays(2, 250m, Unit.Millilitre)];

        Assert.Equal(new Quantity(1250m, Unit.Millilitre), MinimumStockPolicy.TotalStock(batches, Unit.Millilitre));
    }

    [Fact]
    public void TotalStock_IncompatibleBatch_IsNotCounted()
    {
        StockBatch[] batches = [SealedInDays(1, 2m), SealedInDays(1, 1m, Unit.Package)];

        Assert.Equal(new Quantity(2m, Unit.Piece), MinimumStockPolicy.TotalStock(batches, Unit.Piece));
    }

    [Fact]
    public void TotalStock_NoBatches_IsZero() =>
        Assert.True(MinimumStockPolicy.TotalStock([], Unit.Gram).IsZero);

    [Theory]
    [InlineData(499, true)]
    [InlineData(500, false)]
    [InlineData(501, false)]
    public void NeedsRestock_StockAgainstMinimum_OnlyBelowNeedsRestock(decimal grams, bool expected)
    {
        StockBatch[] batches = [SealedInDays(1, grams, Unit.Gram)];

        Assert.Equal(expected, MinimumStockPolicy.NeedsRestock(batches, new Quantity(0.5m, Unit.Kilogram)));
    }

    [Fact]
    public void NeedsRestock_NoBatches_NeedsRestock() =>
        Assert.True(MinimumStockPolicy.NeedsRestock([], new Quantity(1m, Unit.Piece)));

    [Fact]
    public void NeedsRestock_ZeroMinimum_NeverNeedsRestock() =>
        Assert.False(MinimumStockPolicy.NeedsRestock([], Quantity.Zero(Unit.Piece)));

    [Fact]
    public void NeedsRestock_EmptyBatchesOnly_NeedsRestock()
    {
        var batch = SealedInDays(1, 1m);
        batch.Consume(new Quantity(1m, Unit.Piece));

        Assert.True(MinimumStockPolicy.NeedsRestock([batch], new Quantity(1m, Unit.Piece)));
    }

    [Fact]
    public void TotalStock_NullBatches_Throws() =>
        Assert.Throws<ArgumentNullException>(() => MinimumStockPolicy.TotalStock(null!, Unit.Gram));

    [Fact]
    public void NeedsRestock_DefaultMinimum_Throws() =>
        Assert.Throws<ArgumentNullException>(() => MinimumStockPolicy.NeedsRestock([], default));
}
