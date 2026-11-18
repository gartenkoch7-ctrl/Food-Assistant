namespace FoodAssistant.Client.Http;

/// <summary>
/// A failed API call (ADR-0004 ProblemDetails). <paramref name="Status"/> is 0 when no answer arrived.
/// <paramref name="CurrentETag"/> is set on 412 so the caller can reload and retry.
/// </summary>
public sealed record ApiError(
    int Status,
    string ErrorCode,
    string? Detail,
    IReadOnlyDictionary<string, string[]> FieldErrors,
    string? CurrentETag)
{
    public bool IsNetworkError => Status == 0;
}
