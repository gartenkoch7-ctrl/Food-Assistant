namespace FoodAssistant.Server.Api.Idempotency;

/// <summary>
/// Remembers the responses of write requests that carry an <c>Idempotency-Key</c> for
/// <see cref="IdempotencyDefaults.Retention"/> (ADR-0004).
/// </summary>
/// <remarks>
/// Only <see cref="InMemoryIdempotencyStore"/> exists for now: persistence (PostgreSQL) is built in
/// a parallel track of M0. A persistent store replaces it later so that retries also survive a
/// server restart (e.g. an update while the offline queue of a phone still retries). Register it
/// before <c>AddApi</c> or replace the registration; <c>AddApi</c> only adds the in-memory store if none exists.
/// </remarks>
public interface IIdempotencyStore
{
    /// <summary>
    /// Reserves <paramref name="key"/> for a new request, or reports a stored response, a request
    /// still in progress, or a reuse of the key for a different request. A reservation expires
    /// after <see cref="IdempotencyDefaults.ReservationLease"/> if it is never completed.
    /// </summary>
    ValueTask<IdempotencyBeginResult> TryBeginAsync(IdempotencyRequestKey key, string fingerprint, CancellationToken cancellationToken);

    /// <summary>
    /// Stores the response of the reservation <paramref name="reservationId"/>; does nothing if
    /// that reservation no longer exists (lease expired and taken over).
    /// </summary>
    ValueTask CompleteAsync(IdempotencyRequestKey key, Guid reservationId, IdempotentResponse response, CancellationToken cancellationToken);

    /// <summary>Releases the reservation <paramref name="reservationId"/> of a failed request, so a retry runs again.</summary>
    ValueTask AbandonAsync(IdempotencyRequestKey key, Guid reservationId, CancellationToken cancellationToken);
}
