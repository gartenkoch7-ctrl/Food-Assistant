using FoodAssistant.Server.Api.Errors;

namespace FoodAssistant.Server.Auth;

/// <summary>
/// Adapter of the accounts and households endpoints onto <see cref="ApiProblems"/> (ADR-0004), so
/// every error carries the same fields (<c>errorCode</c>, <c>traceId</c>). New code calls
/// <see cref="ApiProblems"/> directly.
/// </summary>
internal static class ProblemResults
{
    /// <param name="statusCode">HTTP status code.</param>
    /// <param name="errorCode">Stable code the client translates.</param>
    /// <param name="message">English explanation for developers.</param>
    /// <param name="hint">Optional additional English hint, appended to <paramref name="message"/>.</param>
    public static IResult Problem(int statusCode, string errorCode, string message, string? hint = null) =>
        ApiProblems.Problem(statusCode, errorCode, hint is null ? message : $"{message} {hint}");

    public static IResult Validation(IReadOnlyDictionary<string, string[]> errors, string errorCode)
    {
        ArgumentNullException.ThrowIfNull(errors);
        return ApiProblems.ValidationFailed(
            errors.ToDictionary(e => e.Key, e => e.Value, StringComparer.Ordinal),
            errorCode: errorCode);
    }
}
