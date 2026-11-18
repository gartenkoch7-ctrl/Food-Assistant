using FoodAssistant.Domain.Shopping;

namespace FoodAssistant.Domain.Tests.Shopping;

public sealed class SectionOrderLearnerTests
{
    private static readonly Dictionary<string, int> NoManual = new(StringComparer.Ordinal);

    [Fact]
    public void WithoutData_IsTheDefaultOrder() =>
        Assert.Equal(StoreSections.All, SectionOrderLearner.Order([], NoManual));

    [Fact]
    public void OneTrip_PutsItsSectionsInWalkingOrder()
    {
        // Beverages at the entrance, then chilled, produce at the end (reverse of the default).
        SectionTransition[] trip =
        [
            new(StoreSections.Beverages, StoreSections.DairyChilled, 1),
            new(StoreSections.DairyChilled, StoreSections.Produce, 1),
        ];

        var order = SectionOrderLearner.Order(trip, NoManual).ToList();

        Assert.Equal(StoreSections.Beverages, order[0]);
        Assert.Equal(StoreSections.Produce, order[^1]);
        Assert.True(order.IndexOf(StoreSections.Beverages) < order.IndexOf(StoreSections.DairyChilled));
        Assert.Equal(StoreSections.All.Count, order.Count);
    }

    [Fact]
    public void ContradictingTrips_TheMoreFrequentDirectionWins()
    {
        SectionTransition[] trips =
        [
            new(StoreSections.Bakery, StoreSections.Produce, 3),
            new(StoreSections.Produce, StoreSections.Bakery, 1),
        ];

        var order = SectionOrderLearner.Order(trips, NoManual).ToList();

        Assert.True(order.IndexOf(StoreSections.Bakery) < order.IndexOf(StoreSections.Produce));
    }

    [Fact]
    public void EqualContradictingTrips_CancelOut()
    {
        SectionTransition[] trips =
        [
            new(StoreSections.Bakery, StoreSections.Produce, 2),
            new(StoreSections.Produce, StoreSections.Bakery, 2),
        ];

        Assert.Equal(StoreSections.All, SectionOrderLearner.Order(trips, NoManual));
    }

    [Fact]
    public void ManualPositions_ComeFirst_RestFollowsLearnedOrder()
    {
        SectionTransition[] trip = [new(StoreSections.Other, StoreSections.Produce, 1)];
        var manual = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            [StoreSections.Frozen] = 2,
            [StoreSections.Beverages] = 1,
        };

        var order = SectionOrderLearner.Order(trip, manual).ToList();

        Assert.Equal([StoreSections.Beverages, StoreSections.Frozen, StoreSections.Other], order.Take(3));
        Assert.Equal(StoreSections.Produce, order[^1]);
        Assert.Equal(StoreSections.All.Count, order.Distinct().Count());
    }

    [Fact]
    public void UnknownKeysAndSelfTransitions_AreIgnored()
    {
        SectionTransition[] transitions =
        [
            new("aisle_7", StoreSections.Produce, 5),
            new(StoreSections.Bakery, "Bakery", 5),
            new(StoreSections.Pantry, StoreSections.Pantry, 5),
        ];
        var manual = new Dictionary<string, int>(StringComparer.Ordinal) { ["aisle_7"] = 0 };

        Assert.Equal(StoreSections.All, SectionOrderLearner.Order(transitions, manual));
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(120, true)]
    [InlineData(121, false)]
    [InlineData(-1, false)]
    public void IsSameTrip_AtMostTwoHoursLater(int minutesLater, bool expected)
    {
        var last = new DateTimeOffset(2026, 11, 18, 10, 0, 0, TimeSpan.Zero);

        Assert.Equal(expected, SectionOrderLearner.IsSameTrip(last, last.AddMinutes(minutesLater)));
    }

    [Fact]
    public void IsSameTrip_WithoutPreviousCheck_IsFalse() =>
        Assert.False(SectionOrderLearner.IsSameTrip(null, DateTimeOffset.UnixEpoch));
}
