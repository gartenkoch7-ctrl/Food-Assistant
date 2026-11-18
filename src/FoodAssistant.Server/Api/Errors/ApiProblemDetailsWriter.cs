using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Options;

namespace FoodAssistant.Server.Api.Errors;

/// <summary>
/// Writes ProblemDetails for API routes regardless of the <c>Accept</c> header. The framework's
/// default writer only answers clients that accept JSON and would otherwise fall back to plain
/// text; API clients must always get <c>application/problem+json</c> (ADR-0004).
/// </summary>
internal sealed class ApiProblemDetailsWriter(
    IOptions<ProblemDetailsOptions> problemDetailsOptions,
    IOptions<JsonOptions> jsonOptions) : IProblemDetailsWriter
{
    private const string ContentType = "application/problem+json";

    public bool CanWrite(ProblemDetailsContext context) => ApiRequests.IsApi(context.HttpContext);

    public ValueTask WriteAsync(ProblemDetailsContext context)
    {
        var httpContext = context.HttpContext;
        var problem = context.ProblemDetails;
        problem.Status ??= httpContext.Response.StatusCode;

        if (problem.Title is null || problem.Type is null)
        {
            // TypedResults.Problem fills the framework's standard title and RFC link for a status code.
            var defaults = TypedResults.Problem(statusCode: problem.Status).ProblemDetails;
            problem.Title ??= defaults.Title;
            problem.Type ??= defaults.Type;
        }

        problemDetailsOptions.Value.CustomizeProblemDetails?.Invoke(context);

        return new ValueTask(httpContext.Response.WriteAsJsonAsync(
            problem,
            problem.GetType(),
            jsonOptions.Value.SerializerOptions,
            ContentType,
            httpContext.RequestAborted));
    }
}
