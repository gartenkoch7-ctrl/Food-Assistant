using FoodAssistant.Contracts.Common;
using FoodAssistant.Contracts.Inventory;
using FoodAssistant.UI.Scan;

namespace FoodAssistant.UI.Tests.Scan;

public sealed class ScanConsumeRuleTests
{
    private static BatchDto Batch(decimal amount, string unit, DateOnly? expires = null) =>
        new(
            Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), new QuantityDto(amount, unit), StorageTypeDto.Chilled,
            BatchStateDto.Sealed, expires, ExpiryKindDto.BestBefore, expires, ExpiryKindDto.BestBefore, null, null, null, null,
            BatchOriginDto.Manual, ExpiryStatusDto.Ok, null, 1);

    [Theory]
    [InlineData("piece")]
    [InlineData("package")]
    [InlineData("can")]
    public void CountedUnits_TakeOne_WhenMoreThanOneIsLeft(string unit)
    {
        var request = ScanConsumeRule.For(Batch(3m, unit));

        Assert.Equal((ConsumptionKindDto.Partial, new QuantityDto(1m, unit), (ConsumptionReasonDto?)null), (request.Kind, request.Quantity, request.Reason));
    }

    [Theory]
    [InlineData(1, "piece")]
    [InlineData(0.5, "package")]
    [InlineData(2, "kg")]
    [InlineData(500, "g")]
    [InlineData(1.5, "l")]
    [InlineData(250, "ml")]
    public void LastOneAndMassOrVolume_UseUpTheWholeBatch(double amount, string unit)
    {
        var request = ScanConsumeRule.For(Batch((decimal)amount, unit));

        Assert.Equal((ConsumptionKindDto.UsedUp, (QuantityDto?)null), (request.Kind, request.Quantity));
    }

    [Fact]
    public void Earliest_IsTheEarliestDate_AndBatchesWithoutDateComeLast()
    {
        var undated = Batch(1m, "piece");
        var late = Batch(1m, "piece", new DateOnly(2026, 12, 1));
        var early = Batch(1m, "piece", new DateOnly(2026, 11, 20));

        Assert.Same(early, ScanConsumeRule.Earliest([undated, late, early]));
        Assert.Same(undated, ScanConsumeRule.Earliest([undated]));
        Assert.Null(ScanConsumeRule.Earliest([]));
    }
}
