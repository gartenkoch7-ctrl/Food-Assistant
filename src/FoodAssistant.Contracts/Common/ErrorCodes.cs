namespace FoodAssistant.Contracts.Common;

/// <summary>
/// Stable <c>errorCode</c> values of ProblemDetails responses shared by all features (ADR-0004).
/// Clients translate these codes; the values never change once released. Features add their own
/// codes in their own Contracts folder, prefixed with the feature name (e.g. <c>inventory.item_not_found</c>).
/// </summary>
public static class ErrorCodes
{
    /// <summary>400: one or more fields are invalid; <c>errors</c> lists them per field.</summary>
    public const string ValidationFailed = "common.validation_failed";

    /// <summary>400: the request is malformed (e.g. unreadable JSON, wrong parameter format).</summary>
    public const string BadRequest = "common.bad_request";

    /// <summary>401: not signed in.</summary>
    public const string Unauthorized = "common.unauthorized";

    /// <summary>403: signed in, but not allowed.</summary>
    public const string Forbidden = "common.forbidden";

    /// <summary>404: does not exist or belongs to another household (no hint which).</summary>
    public const string NotFound = "common.not_found";

    /// <summary>405: the HTTP method is not supported on this route.</summary>
    public const string MethodNotAllowed = "common.method_not_allowed";

    /// <summary>409: conflicts with the current state.</summary>
    public const string Conflict = "common.conflict";

    /// <summary>412: <c>If-Match</c> does not match the current version; the response carries the current <c>ETag</c>.</summary>
    public const string PreconditionFailed = "common.precondition_failed";

    /// <summary>428: a change to a versioned resource was sent without <c>If-Match</c>.</summary>
    public const string PreconditionRequired = "common.precondition_required";

    /// <summary>413: the request body is too large.</summary>
    public const string PayloadTooLarge = "common.payload_too_large";

    /// <summary>415: the content type is not supported.</summary>
    public const string UnsupportedMediaType = "common.unsupported_media_type";

    /// <summary>422: a business rule was violated.</summary>
    public const string RuleViolated = "common.rule_violated";

    /// <summary>429: too many requests; retry after the <c>Retry-After</c> header.</summary>
    public const string RateLimited = "common.rate_limited";

    /// <summary>400: the <c>Idempotency-Key</c> header is not a single UUID.</summary>
    public const string IdempotencyKeyInvalid = "common.idempotency_key_invalid";

    /// <summary>422: the <c>Idempotency-Key</c> was already used for a different request.</summary>
    public const string IdempotencyKeyReused = "common.idempotency_key_reused";

    /// <summary>409: a request with the same <c>Idempotency-Key</c> is still being processed.</summary>
    public const string IdempotencyRequestInProgress = "common.idempotency_request_in_progress";

    /// <summary>400: the pagination <c>cursor</c> is invalid or expired.</summary>
    public const string CursorInvalid = "common.cursor_invalid";

    /// <summary>Any other 4xx response without a more specific code.</summary>
    public const string RequestFailed = "common.request_failed";

    /// <summary>500: unexpected server error; the response contains no internal details.</summary>
    public const string InternalError = "common.internal_error";

    /// <summary>503: the server or a required service is temporarily unavailable.</summary>
    public const string ServiceUnavailable = "common.service_unavailable";
}
