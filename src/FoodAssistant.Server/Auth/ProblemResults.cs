namespace FoodAssistant.Server.Auth;

/// <summary>
/// ProblemDetails with <c>errorCode</c> (ADR-0004). Interim helper of the accounts track until the
/// shared API helpers of the API track are merged; then these call sites move over.
/// </summary>
internal static class ProblemResults
{
    public const string ErrorCodeKey = "errorCode";

    public static IResult Problem(int statusCode, string errorCode, string title, string? detail = null) =>
        Results.Problem(
            statusCode: statusCode,
            title: title,
            detail: detail,
            extensions: new Dictionary<string, object?>(StringComparer.Ordinal) { [ErrorCodeKey] = errorCode });

    public static IResult Validation(IReadOnlyDictionary<string, string[]> errors, string errorCode) =>
        Results.ValidationProblem(
            errors.ToDictionary(e => e.Key, e => e.Value, StringComparer.Ordinal),
            extensions: new Dictionary<string, object?>(StringComparer.Ordinal) { [ErrorCodeKey] = errorCode });
}
