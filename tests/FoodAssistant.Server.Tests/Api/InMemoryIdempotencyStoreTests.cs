using FoodAssistant.Server.Api.Idempotency;

namespace FoodAssistant.Server.Tests.Api;

public sealed class InMemoryIdempotencyStoreTests
{
    private static readonly IdempotentResponse Response =
        new(201, new Dictionary<string, string> { ["Location"] = "/x" }, new byte[] { 1, 2, 3 });

    private readonly ManualTimeProvider _time = new();
    private readonly IdempotencyRequestKey _key = new("person:1", "/API/V1/ITEMS", Guid.NewGuid());

    [Fact]
    public async Task FirstRequest_StartsThenReplaysCompletedResponse()
    {
        var store = new InMemoryIdempotencyStore(_time);

        var first = await store.TryBeginAsync(_key, "hash", CancellationToken.None);
        var whileRunning = await store.TryBeginAsync(_key, "hash", CancellationToken.None);
        await store.CompleteAsync(_key, first.ReservationId, Response, CancellationToken.None);
        var retry = await store.TryBeginAsync(_key, "hash", CancellationToken.None);

        Assert.Equal(IdempotencyBeginOutcome.Started, first.Outcome);
        Assert.Equal(IdempotencyBeginOutcome.InProgress, whileRunning.Outcome);
        Assert.Equal(IdempotencyBeginOutcome.Completed, retry.Outcome);
        Assert.Same(Response, retry.Response);
    }

    [Fact]
    public async Task DifferentFingerprint_IsMismatch()
    {
        var store = new InMemoryIdempotencyStore(_time);
        await CompleteAsync(store, _key);

        var result = await store.TryBeginAsync(_key, "other", CancellationToken.None);

        Assert.Equal(IdempotencyBeginOutcome.FingerprintMismatch, result.Outcome);
    }

    [Fact]
    public async Task OtherCaller_IsIndependent()
    {
        var store = new InMemoryIdempotencyStore(_time);
        await store.TryBeginAsync(_key, "hash", CancellationToken.None);

        var result = await store.TryBeginAsync(_key with { Caller = "person:2" }, "hash", CancellationToken.None);

        Assert.Equal(IdempotencyBeginOutcome.Started, result.Outcome);
    }

    [Fact]
    public async Task Abandoned_CanStartAgain()
    {
        var store = new InMemoryIdempotencyStore(_time);
        var first = await store.TryBeginAsync(_key, "hash", CancellationToken.None);
        await store.AbandonAsync(_key, first.ReservationId, CancellationToken.None);

        var result = await store.TryBeginAsync(_key, "hash", CancellationToken.None);

        Assert.Equal(IdempotencyBeginOutcome.Started, result.Outcome);
    }

    [Fact]
    public async Task ExpiredReservation_IsTakenOverAndOldOwnerCannotTouchIt()
    {
        var store = new InMemoryIdempotencyStore(_time);
        var slow = await store.TryBeginAsync(_key, "hash", CancellationToken.None);

        _time.Advance(IdempotencyDefaults.ReservationLease + TimeSpan.FromSeconds(1));
        var retry = await store.TryBeginAsync(_key, "hash", CancellationToken.None);
        await store.AbandonAsync(_key, slow.ReservationId, CancellationToken.None);
        await store.CompleteAsync(_key, slow.ReservationId, Response, CancellationToken.None);
        var third = await store.TryBeginAsync(_key, "hash", CancellationToken.None);

        Assert.Equal(IdempotencyBeginOutcome.Started, retry.Outcome);
        Assert.Equal(IdempotencyBeginOutcome.InProgress, third.Outcome);
    }

    [Fact]
    public async Task AfterRetention_KeyStartsFresh()
    {
        var store = new InMemoryIdempotencyStore(_time);
        await CompleteAsync(store, _key);

        _time.Advance(IdempotencyDefaults.Retention - TimeSpan.FromMinutes(1));
        var withinRetention = await store.TryBeginAsync(_key, "other", CancellationToken.None);
        _time.Advance(TimeSpan.FromMinutes(2));
        var afterRetention = await store.TryBeginAsync(_key, "other", CancellationToken.None);

        Assert.Equal(IdempotencyBeginOutcome.FingerprintMismatch, withinRetention.Outcome);
        Assert.Equal(IdempotencyBeginOutcome.Started, afterRetention.Outcome);
        Assert.Equal(1, store.Count);
        Assert.Equal(0, store.StoredBytes);
    }

    [Fact]
    public async Task ExpiredEntriesOfOtherKeys_AreSwept()
    {
        var store = new InMemoryIdempotencyStore(_time);
        await CompleteAsync(store, _key);

        _time.Advance(IdempotencyDefaults.Retention + TimeSpan.FromHours(1));
        await store.TryBeginAsync(_key with { Key = Guid.NewGuid() }, "hash", CancellationToken.None);

        Assert.Equal(1, store.Count);
    }

    [Fact]
    public async Task WhenFull_OldestCompletedEntriesAreEvicted()
    {
        var store = new InMemoryIdempotencyStore(_time);
        var first = _key with { Key = Guid.NewGuid() };
        await CompleteAsync(store, first);
        for (var i = 1; i < InMemoryIdempotencyStore.MaxEntries; i++)
        {
            _time.Advance(TimeSpan.FromMilliseconds(1));
            await CompleteAsync(store, _key with { Key = Guid.NewGuid() });
        }

        await store.TryBeginAsync(_key with { Key = Guid.NewGuid() }, "hash", CancellationToken.None);
        var oldest = await store.TryBeginAsync(first, "hash", CancellationToken.None);

        Assert.True(store.Count <= (InMemoryIdempotencyStore.MaxEntries * 9 / 10) + 2);
        Assert.Equal(IdempotencyBeginOutcome.Started, oldest.Outcome);
    }

    private static async Task CompleteAsync(InMemoryIdempotencyStore store, IdempotencyRequestKey key)
    {
        var begin = await store.TryBeginAsync(key, "hash", CancellationToken.None);
        await store.CompleteAsync(key, begin.ReservationId, Response, CancellationToken.None);
    }

    private sealed class ManualTimeProvider : TimeProvider
    {
        private DateTimeOffset _now = new(2026, 11, 18, 12, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan by) => _now += by;
    }
}
