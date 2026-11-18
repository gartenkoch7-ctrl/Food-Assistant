namespace FoodAssistant.Server.Api.Idempotency;

/// <summary>Result kind of <see cref="IIdempotencyStore.TryBeginAsync"/>.</summary>
public enum IdempotencyBeginOutcome
{
    /// <summary>Key reserved; run the request and store its response.</summary>
    Started,

    /// <summary>Same request seen before; replay the stored response.</summary>
    Completed,

    /// <summary>Same request is still running (concurrent retry).</summary>
    InProgress,

    /// <summary>Key already used for a request with a different method, query or body.</summary>
    FingerprintMismatch,
}
