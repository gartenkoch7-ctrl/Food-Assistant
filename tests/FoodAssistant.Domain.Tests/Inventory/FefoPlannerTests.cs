using FoodAssistant.Domain.Inventory;
using FoodAssistant.Domain.Quantities;
using static FoodAssistant.Domain.Tests.Inventory.TestBatches;

namespace FoodAssistant.Domain.Tests.Inventory;

public sealed class FefoPlannerTests
{
    [Fact]
    public void Plan_SeveralBatches_TakesEarliestDateFirst()
    {
        var late = SealedInDays(9, 2m);
        var early = SealedInDays(1, 2m);
        var middle = SealedInDays(4, 2m);

        var plan = FefoPlanner.Plan([late, early, middle], new Quantity(3m, Unit.Piece));

        Assert.Equal(
            [new FefoAllocation(early.Id, new Quantity(2m, Unit.Piece)), new FefoAllocation(middle.Id, new Quantity(1m, Unit.Piece))],
            plan.Allocations);
        Assert.True(plan.IsFullyCovered);
        Assert.True(plan.Shortfall.IsZero);
    }

    [Fact]
    public void Plan_BatchWithoutDate_IsUsedLast()
    {
        var noDate = Sealed(null, amount: 5m);
        var dated = SealedInDays(30, 1m);

        var plan = FefoPlanner.Plan([noDate, dated], new Quantity(2m, Unit.Piece));

        Assert.Equal([dated.Id, noDate.Id], plan.Allocations.Select(a => a.BatchId));
        Assert.Equal(new Quantity(1m, Unit.Piece), plan.Allocations[1].Quantity);
    }

    [Fact]
    public void Plan_ExpiredBatch_IsUsedFirst()
    {
        var expired = SealedInDays(-2, 1m);
        var fresh = SealedInDays(5, 1m);

        var plan = FefoPlanner.Plan([fresh, expired], new Quantity(1m, Unit.Piece));

        Assert.Equal(expired.Id, Assert.Single(plan.Allocations).BatchId);
    }

    [Fact]
    public void Plan_SameDate_KeepsInputOrder()
    {
        var first = SealedInDays(3, 1m);
        var second = SealedInDays(3, 1m);

        var plan = FefoPlanner.Plan([first, second], new Quantity(1m, Unit.Piece));

        Assert.Equal(first.Id, Assert.Single(plan.Allocations).BatchId);
    }

    [Fact]
    public void Plan_NotEnoughStock_ReportsShortfall()
    {
        var batch = SealedInDays(3, 2m);

        var plan = FefoPlanner.Plan([batch], new Quantity(5m, Unit.Piece));

        Assert.False(plan.IsFullyCovered);
        Assert.Equal(new Quantity(3m, Unit.Piece), plan.Shortfall);
        Assert.Equal(new Quantity(2m, Unit.Piece), Assert.Single(plan.Allocations).Quantity);
    }

    [Fact]
    public void Plan_NoBatches_ShortfallIsWholeAmount()
    {
        var plan = FefoPlanner.Plan([], new Quantity(250m, Unit.Gram));

        Assert.Empty(plan.Allocations);
        Assert.Equal(new Quantity(250m, Unit.Gram), plan.Shortfall);
    }

    [Fact]
    public void Plan_MixedUnits_AllocatesInBatchUnit()
    {
        var kilo = SealedInDays(1, 1m, Unit.Kilogram);
        var grams = SealedInDays(2, 500m, Unit.Gram);

        var plan = FefoPlanner.Plan([grams, kilo], new Quantity(1200m, Unit.Gram));

        Assert.Equal(
            [new FefoAllocation(kilo.Id, new Quantity(1m, Unit.Kilogram)), new FefoAllocation(grams.Id, new Quantity(200m, Unit.Gram))],
            plan.Allocations);
        Assert.True(plan.IsFullyCovered);
    }

    [Fact]
    public void Plan_ApproximateUnits_LeavesNoRoundingShortfall()
    {
        var first = SealedInDays(1, 20m, Unit.Millilitre);
        var second = SealedInDays(2, 10m, Unit.Millilitre);

        var plan = FefoPlanner.Plan([first, second], new Quantity(2m, Unit.Tablespoon));

        Assert.True(plan.IsFullyCovered);
        Assert.Equal(2, plan.Allocations.Count);
    }

    [Fact]
    public void Plan_IncompatibleAndEmptyBatches_AreSkipped()
    {
        var other = SealedInDays(0, 1m, Unit.Can);
        var empty = SealedInDays(0, 1m);
        empty.Consume(new Quantity(1m, Unit.Piece));
        var usable = SealedInDays(5, 3m);

        var plan = FefoPlanner.Plan([other, empty, usable], new Quantity(1m, Unit.Piece));

        Assert.Equal(usable.Id, Assert.Single(plan.Allocations).BatchId);
    }

    [Fact]
    public void Plan_PlanApplied_ConsumesBatches()
    {
        var early = SealedInDays(1, 2m);
        var late = SealedInDays(2, 2m);
        var batches = new[] { early, late };

        var plan = FefoPlanner.Plan(batches, new Quantity(3m, Unit.Piece));
        foreach (var allocation in plan.Allocations)
        {
            batches.Single(b => b.Id == allocation.BatchId).Consume(allocation.Quantity);
        }

        Assert.True(early.IsEmpty);
        Assert.Equal(new Quantity(1m, Unit.Piece), late.Quantity);
    }

    [Fact]
    public void Plan_ZeroAmount_Throws() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => FefoPlanner.Plan([], Quantity.Zero(Unit.Piece)));

    [Fact]
    public void Plan_NullBatches_Throws() =>
        Assert.Throws<ArgumentNullException>(() => FefoPlanner.Plan(null!, new Quantity(1m, Unit.Piece)));
}
