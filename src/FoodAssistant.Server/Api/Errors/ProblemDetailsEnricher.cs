using System.Diagnostics;
using FoodAssistant.Contracts.Common;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Options;

namespace FoodAssistant.Server.Api.Errors;

/// <summary>
/// Adds <c>errorCode</c> and <c>traceId</c> to every ProblemDetails the server writes, including
/// problems created by the framework (validation, status code pages, rate limiter).
/// </summary>
internal static class ProblemDetailsEnricher
{
    public static void Enrich(ProblemDetailsContext context)
    {
        var problem = context.ProblemDetails;
        var status = problem.Status ?? context.HttpContext.Response.StatusCode;

        if (!problem.Extensions.ContainsKey(ApiProblems.ErrorCodeField))
        {
            problem.Extensions[ApiProblems.ErrorCodeField] =
                problem is HttpValidationProblemDetails && status == StatusCodes.Status400BadRequest
                    ? ErrorCodes.ValidationFailed
                    : StatusErrorCodes.For(status);
        }

        // Same value ASP.NET Core uses by default: W3C trace parent id, so it matches exported traces.
        problem.Extensions.TryAdd(ApiProblems.TraceIdField, Activity.Current?.Id ?? context.HttpContext.TraceIdentifier);

        if (problem is HttpValidationProblemDetails validation
            && context.HttpContext.RequestServices.GetService<IOptions<JsonOptions>>()?.Value.SerializerOptions.PropertyNamingPolicy is { } namingPolicy)
        {
            ValidationErrorKeys.ApplyNamingPolicy(validation, namingPolicy);
        }
    }
}
