namespace FoodAssistant.Client.Http;

/// <summary>Outcome of an API call that returns a value; errors are values, not exceptions.</summary>
public sealed record ApiResult<T>(T? Value, string? ETag, ApiError? Error)
{
    public bool IsSuccess => Error is null;

    // Factories on the generic result type read best at call sites (ApiResult<T>.Success), so CA1000 is waived here.
#pragma warning disable CA1000
    public static ApiResult<T> Success(T value, string? eTag = null) => new(value, eTag, null);

    public static ApiResult<T> Failure(ApiError error) => new(default, null, error);
#pragma warning restore CA1000
}

/// <summary>Outcome of an API call without a response body.</summary>
public sealed record ApiResult(string? ETag, ApiError? Error)
{
    public bool IsSuccess => Error is null;

    public static ApiResult Success(string? eTag = null) => new(eTag, null);

    public static ApiResult Failure(ApiError error) => new(null, error);
}
