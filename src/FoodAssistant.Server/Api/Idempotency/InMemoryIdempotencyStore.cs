using System.Collections.Concurrent;

namespace FoodAssistant.Server.Api.Idempotency;

/// <summary>
/// Process-local <see cref="IIdempotencyStore"/>. Entries are lost on restart and not shared
/// between server instances; acceptable for a single self-hosted instance until the persistent
/// store exists (see <see cref="IIdempotencyStore"/>).
/// </summary>
/// <remarks>
/// Memory is bounded by <see cref="MaxEntries"/> and <see cref="MaxStoredBytes"/>. When either is
/// reached, the completed entries closest to expiry are dropped first; that only happens far
/// beyond the write volume of a household.
/// </remarks>
public sealed class InMemoryIdempotencyStore(TimeProvider timeProvider) : IIdempotencyStore
{
    /// <summary>Upper bound of stored entries.</summary>
    public const int MaxEntries = 5_000;

    /// <summary>Upper bound of all stored response bodies together.</summary>
    public const long MaxStoredBytes = 64L * 1024 * 1024;

    private static readonly TimeSpan SweepInterval = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan EvictionInterval = TimeSpan.FromSeconds(10);

    private readonly ConcurrentDictionary<IdempotencyRequestKey, Entry> _entries = new();
    private readonly Lock _evictionLock = new();
    private long _nextSweepTicks;
    private long _nextEvictionTicks;

    // Own counters: ConcurrentDictionary.Count takes every bucket lock.
    private int _count;
    private long _bytes;

    /// <summary>Number of stored entries, including expired ones not yet swept.</summary>
    public int Count => Volatile.Read(ref _count);

    /// <summary>Total size of the stored response bodies.</summary>
    public long StoredBytes => Interlocked.Read(ref _bytes);

    public ValueTask<IdempotencyBeginResult> TryBeginAsync(IdempotencyRequestKey key, string fingerprint, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(fingerprint);
        var now = timeProvider.GetUtcNow();
        SweepIfDue(now);

        var reservation = new Entry(Guid.NewGuid(), fingerprint, now + IdempotencyDefaults.ReservationLease, Response: null);
        while (true)
        {
            var entry = _entries.GetOrAdd(key, reservation);
            if (ReferenceEquals(entry, reservation))
            {
                Interlocked.Increment(ref _count);
                return Started(reservation, now);
            }

            if (entry.ExpiresAt <= now)
            {
                if (_entries.TryUpdate(key, reservation, entry))
                {
                    Interlocked.Add(ref _bytes, -entry.Size);
                    return Started(reservation, now);
                }

                continue; // Another request replaced the expired entry first; evaluate the new one.
            }

            var result = !string.Equals(entry.Fingerprint, fingerprint, StringComparison.Ordinal)
                ? new IdempotencyBeginResult(IdempotencyBeginOutcome.FingerprintMismatch)
                : entry.Response is null
                    ? new IdempotencyBeginResult(IdempotencyBeginOutcome.InProgress)
                    : new IdempotencyBeginResult(IdempotencyBeginOutcome.Completed, entry.Response);
            return ValueTask.FromResult(result);
        }
    }

    public ValueTask CompleteAsync(IdempotencyRequestKey key, Guid reservationId, IdempotentResponse response, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(response);
        if (_entries.TryGetValue(key, out var entry) && entry.Response is null && entry.ReservationId == reservationId)
        {
            // Retention starts when the answer exists: a slow request must not shorten the replay window.
            var completed = entry with { Response = response, ExpiresAt = timeProvider.GetUtcNow() + IdempotencyDefaults.Retention };
            if (_entries.TryUpdate(key, completed, entry))
            {
                Interlocked.Add(ref _bytes, completed.Size);
            }
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask AbandonAsync(IdempotencyRequestKey key, Guid reservationId, CancellationToken cancellationToken)
    {
        if (_entries.TryGetValue(key, out var entry) && entry.Response is null && entry.ReservationId == reservationId)
        {
            Remove(KeyValuePair.Create(key, entry));
        }

        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Evicts only after the lookup: a retry that is about to be replayed must not lose its entry
    /// to the capacity limit. The new reservation itself is running and never evicted.
    /// </summary>
    private ValueTask<IdempotencyBeginResult> Started(Entry reservation, DateTimeOffset now)
    {
        // At most one eviction pass per interval: if running requests keep the store over its
        // limit, every request would otherwise scan and sort all entries under the lock.
        var due = Interlocked.Read(ref _nextEvictionTicks);
        if ((Count > MaxEntries || StoredBytes > MaxStoredBytes)
            && now.UtcTicks >= due
            && Interlocked.CompareExchange(ref _nextEvictionTicks, (now + EvictionInterval).UtcTicks, due) == due)
        {
            Evict(now);
        }

        return ValueTask.FromResult(new IdempotencyBeginResult(IdempotencyBeginOutcome.Started, ReservationId: reservation.ReservationId));
    }

    private void SweepIfDue(DateTimeOffset now)
    {
        var due = Interlocked.Read(ref _nextSweepTicks);
        if (now.UtcTicks < due
            || Interlocked.CompareExchange(ref _nextSweepTicks, (now + SweepInterval).UtcTicks, due) != due)
        {
            return;
        }

        RemoveExpired(now);
    }

    private void Evict(DateTimeOffset now)
    {
        lock (_evictionLock)
        {
            RemoveExpired(now);
            if (Count <= MaxEntries && StoredBytes <= MaxStoredBytes)
            {
                return;
            }

            // Down to 90 % of both limits. Running requests are never dropped; their number is
            // bounded by concurrent connections.
            var entryTarget = MaxEntries * 9 / 10;
            var byteTarget = MaxStoredBytes * 9 / 10;
            foreach (var pair in _entries.Where(pair => pair.Value.Response is not null).OrderBy(pair => pair.Value.ExpiresAt).ToList())
            {
                if (Count <= entryTarget && StoredBytes <= byteTarget)
                {
                    break;
                }

                Remove(pair);
            }
        }
    }

    private void RemoveExpired(DateTimeOffset now)
    {
        foreach (var pair in _entries)
        {
            if (pair.Value.ExpiresAt <= now)
            {
                Remove(pair);
            }
        }
    }

    private void Remove(KeyValuePair<IdempotencyRequestKey, Entry> pair)
    {
        if (_entries.TryRemove(pair))
        {
            Interlocked.Decrement(ref _count);
            Interlocked.Add(ref _bytes, -pair.Value.Size);
        }
    }

    private sealed record Entry(Guid ReservationId, string Fingerprint, DateTimeOffset ExpiresAt, IdempotentResponse? Response)
    {
        public long Size => Response?.Body.Length ?? 0;
    }
}
