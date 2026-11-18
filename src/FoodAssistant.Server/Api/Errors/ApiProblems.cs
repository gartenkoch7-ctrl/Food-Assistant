using FoodAssistant.Contracts.Common;
using Microsoft.AspNetCore.Http.HttpResults;

namespace FoodAssistant.Server.Api.Errors;

/// <summary>
/// The only way endpoints return errors (ADR-0004): RFC 9457 ProblemDetails with a stable
/// <c>errorCode</c>. <c>traceId</c> is added centrally when the response is written.
/// </summary>
public static class ApiProblems
{
    /// <summary>Extension field with the stable, translatable error code.</summary>
    public const string ErrorCodeField = "errorCode";

    /// <summary>Extension field with the trace id, so support can find the request in logs and traces.</summary>
    public const string TraceIdField = "traceId";

    /// <summary>Generic problem; prefer the specific helpers below.</summary>
    /// <param name="statusCode">HTTP status code (4xx/5xx).</param>
    /// <param name="errorCode">Stable code, e.g. <see cref="ErrorCodes.NotFound"/> or a feature code.</param>
    /// <param name="detail">Optional English explanation for developers; clients show texts based on <paramref name="errorCode"/>.</param>
    /// <param name="extensions">Additional fields; must not contain internal details.</param>
    public static ProblemHttpResult Problem(
        int statusCode,
        string errorCode,
        string? detail = null,
        IEnumerable<KeyValuePair<string, object?>>? extensions = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(errorCode);
        var fields = new Dictionary<string, object?>(StringComparer.Ordinal);
        if (extensions is not null)
        {
            foreach (var (key, value) in extensions)
            {
                fields[key] = value;
            }
        }

        fields[ErrorCodeField] = errorCode;
        return TypedResults.Problem(detail: detail, statusCode: statusCode, extensions: fields);
    }

    /// <summary>400 for malformed requests that are not field validation errors.</summary>
    public static ProblemHttpResult BadRequest(string errorCode = ErrorCodes.BadRequest, string? detail = null) =>
        Problem(StatusCodes.Status400BadRequest, errorCode, detail);

    /// <summary>400 with field errors (<c>errors</c>: field name → messages).</summary>
    public static ValidationProblem ValidationFailed(IDictionary<string, string[]> errors, string? detail = null)
    {
        ArgumentNullException.ThrowIfNull(errors);
        return TypedResults.ValidationProblem(
            errors,
            detail: detail,
            extensions: new Dictionary<string, object?>(StringComparer.Ordinal) { [ErrorCodeField] = ErrorCodes.ValidationFailed });
    }

    /// <summary>404, also for resources of another household (no hint that they exist).</summary>
    public static ProblemHttpResult NotFound(string errorCode = ErrorCodes.NotFound, string? detail = null) =>
        Problem(StatusCodes.Status404NotFound, errorCode, detail);

    /// <summary>409 for conflicts with the current state.</summary>
    public static ProblemHttpResult Conflict(string errorCode = ErrorCodes.Conflict, string? detail = null) =>
        Problem(StatusCodes.Status409Conflict, errorCode, detail);

    /// <summary>422 when a business rule is violated.</summary>
    public static ProblemHttpResult RuleViolated(string errorCode = ErrorCodes.RuleViolated, string? detail = null) =>
        Problem(StatusCodes.Status422UnprocessableEntity, errorCode, detail);
}
