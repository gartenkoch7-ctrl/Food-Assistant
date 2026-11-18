namespace FoodAssistant.Server.Api.Idempotency;

/// <summary>Answer of <see cref="IIdempotencyStore.TryBeginAsync"/>.</summary>
/// <param name="Outcome">What the caller has to do.</param>
/// <param name="Response">Stored response; set only for <see cref="IdempotencyBeginOutcome.Completed"/>.</param>
/// <param name="ReservationId">
/// Identifies this caller's reservation for <see cref="IdempotencyBeginOutcome.Started"/>; complete
/// or abandon with it, so a request that outlived its lease cannot touch a newer reservation.
/// </param>
public readonly record struct IdempotencyBeginResult(
    IdempotencyBeginOutcome Outcome,
    IdempotentResponse? Response = null,
    Guid ReservationId = default);
