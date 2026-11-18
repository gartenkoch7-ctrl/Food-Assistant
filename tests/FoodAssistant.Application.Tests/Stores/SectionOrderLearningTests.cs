using FoodAssistant.Application.Stores;
using FoodAssistant.Domain.Shopping;

namespace FoodAssistant.Application.Tests.Stores;

/// <summary>Which transition is counted, and that a lost race is decided again from the newer check-off.</summary>
public sealed class SectionOrderLearningTests
{
    private static readonly Guid StoreId = Guid.CreateVersion7();
    private static readonly DateTimeOffset Now = new(2026, 11, 18, 10, 0, 0, TimeSpan.Zero);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task CheckWithinTwoHoursInAnotherSection_CountsTheTransition()
    {
        var store = new FakeOrderStore((StoreSections.Produce, Now.AddMinutes(-10)));

        await new SectionOrderLearning(store).RecordCheckAsync(StoreId, StoreSections.Bakery, Now, Ct);

        Assert.Equal([(StoreSections.Produce, StoreSections.Bakery)], store.Recorded);
    }

    [Theory]
    [InlineData(StoreSections.Bakery, -10)]
    [InlineData(StoreSections.Produce, -121)]
    public async Task SameSectionOrNewTrip_CountsNothing(string lastSection, int minutes)
    {
        var store = new FakeOrderStore((lastSection, Now.AddMinutes(minutes)));

        await new SectionOrderLearning(store).RecordCheckAsync(StoreId, StoreSections.Bakery, Now, Ct);

        Assert.Equal([((string?)null, StoreSections.Bakery)], store.Recorded);
    }

    [Fact]
    public async Task LostRace_DecidesAgainFromTheNewerCheckOff()
    {
        var store = new FakeOrderStore((StoreSections.Produce, Now.AddMinutes(-10)))
        {
            // Someone checked off frozen goods right before us.
            Interference = (StoreSections.Frozen, Now.AddMinutes(-1)),
        };

        await new SectionOrderLearning(store).RecordCheckAsync(StoreId, StoreSections.Bakery, Now, Ct);

        Assert.Equal([(StoreSections.Frozen, StoreSections.Bakery)], store.Recorded);
    }

    private sealed class FakeOrderStore((string? SectionKey, DateTimeOffset? At) last) : IStoreSectionOrderStore
    {
        private (string? SectionKey, DateTimeOffset? At) _last = last;

        public (string? SectionKey, DateTimeOffset? At)? Interference { get; set; }

        public List<(string? From, string To)> Recorded { get; } = [];

        public Task<(string? SectionKey, DateTimeOffset? At)?> GetLastCheckAsync(Guid storeId, CancellationToken cancellationToken) =>
            Task.FromResult<(string? SectionKey, DateTimeOffset? At)?>(_last);

        public Task<bool> RecordTransitionAsync(
            Guid storeId,
            (string? SectionKey, DateTimeOffset? At) expectedLast,
            string? from,
            string to,
            DateTimeOffset at,
            CancellationToken cancellationToken)
        {
            if (Interference is { } other)
            {
                _last = other;
                Interference = null;
            }

            if (expectedLast != _last)
            {
                return Task.FromResult(false);
            }

            Recorded.Add((from, to));
            _last = (to, at);
            return Task.FromResult(true);
        }

        public Task<IReadOnlyList<SectionTransition>> GetTransitionsAsync(Guid storeId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyDictionary<string, int>> GetManualOrderAsync(Guid storeId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<bool> SetManualOrderAsync(Guid storeId, IReadOnlyList<string> sections, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task ClearManualOrderAsync(Guid storeId, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
