using FoodAssistant.Application.Common;
using FoodAssistant.Server.Api.Concurrency;
using FoodAssistant.Server.Api.Errors;

namespace FoodAssistant.Server.Api.Results;

/// <summary>
/// The one mapping of <see cref="OperationResult{T}"/> to HTTP (ADR-0004); endpoints never map
/// errors themselves.
/// </summary>
public static class OperationResults
{
    /// <param name="result">Result of the use case.</param>
    /// <param name="http">Current request (ETag header, 412 problem).</param>
    /// <param name="versionOf">Version of the value, sent as <c>ETag</c> on 200 and 201.</param>
    /// <param name="locationOf">URL of a created value for the <c>Location</c> header of 201.</param>
    public static IResult ToHttpResult<T>(
        this OperationResult<T> result,
        HttpContext http,
        Func<T, long>? versionOf = null,
        Func<T, string>? locationOf = null)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(http);

        return result.Status switch
        {
            OperationStatus.Ok => Success(result.Value, http, versionOf, value => TypedResults.Ok(value)),
            OperationStatus.Created => Success(
                result.Value,
                http,
                versionOf,
                value => TypedResults.Created(value is null ? null : locationOf?.Invoke(value), value)),
            OperationStatus.Deleted => TypedResults.NoContent(),
            OperationStatus.NotFound => ApiProblems.NotFound(Code(result)),
            OperationStatus.Invalid => ApiProblems.ValidationFailed(
                result.Errors?.ToDictionary(e => e.Key, e => e.Value, StringComparer.Ordinal)
                    ?? new Dictionary<string, string[]>(StringComparer.Ordinal),
                errorCode: Code(result)),
            OperationStatus.Conflict => ApiProblems.Conflict(Code(result)),
            OperationStatus.RuleViolated => ApiProblems.RuleViolated(Code(result)),
            OperationStatus.VersionMismatch => ETags.PreconditionFailed(
                http,
                result.CurrentVersion ?? throw new InvalidOperationException("A version mismatch needs the current version."),
                result.Value),
            OperationStatus.Forbidden => ApiProblems.Problem(StatusCodes.Status403Forbidden, Code(result)),
            _ => throw new InvalidOperationException($"Unknown operation status {result.Status}."),
        };
    }

    private static IResult Success<T>(T? value, HttpContext http, Func<T, long>? versionOf, Func<T?, IResult> respond)
    {
        if (value is not null && versionOf is not null)
        {
            http.Response.SetETag(versionOf(value));
        }

        return respond(value);
    }

    // The failure factories of OperationResult require a code.
    private static string Code<T>(OperationResult<T> result) => result.ErrorCode!;
}
