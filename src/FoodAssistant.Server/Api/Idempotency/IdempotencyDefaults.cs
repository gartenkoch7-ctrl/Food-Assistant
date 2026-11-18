namespace FoodAssistant.Server.Api.Idempotency;

/// <summary>Fixed values of the idempotency convention (ADR-0004).</summary>
public static class IdempotencyDefaults
{
    /// <summary>Request header carrying a client-generated UUID.</summary>
    public const string HeaderName = "Idempotency-Key";

    /// <summary>Response header set to <c>true</c> when the response is a stored replay.</summary>
    public const string ReplayedHeaderName = "Idempotent-Replayed";

    /// <summary>
    /// Largest response body that is stored. Write endpoints answer with small DTOs; for a larger
    /// answer only status and headers are stored, so a retry gets e.g. 201 with <c>Location</c>
    /// but no body and reloads the resource.
    /// </summary>
    public const int MaxStoredResponseBytes = 1024 * 1024;

    /// <summary>How long a stored response is replayed.</summary>
    public static readonly TimeSpan Retention = TimeSpan.FromHours(24);

    /// <summary>
    /// How long a reservation of a running request blocks retries (409). Longer than any request
    /// may run, short enough that a reservation left behind (crash, failed store write) does not
    /// block the key for a whole day.
    /// </summary>
    public static readonly TimeSpan ReservationLease = TimeSpan.FromMinutes(10);
}
