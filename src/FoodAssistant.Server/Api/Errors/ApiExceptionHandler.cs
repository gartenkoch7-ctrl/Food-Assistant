using Microsoft.AspNetCore.Diagnostics;

namespace FoodAssistant.Server.Api.Errors;

/// <summary>
/// Turns exceptions on API routes into ProblemDetails. Unexpected exceptions become a plain 500
/// without message or stack trace; the details only go to the log (ADR-0004).
/// </summary>
internal sealed class ApiExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (!ApiRequests.IsApi(httpContext))
        {
            return false;
        }

        int status;
        if (exception is BadHttpRequestException badRequest)
        {
            // Unreadable body, wrong parameter format, body too large: the client's fault, not ours.
            status = badRequest.StatusCode;
            if (logger.IsEnabled(LogLevel.Debug))
            {
                logger.BadRequestRejected(status, httpContext.Request.Method, ApiRequests.RouteTemplate(httpContext));
            }
        }
        else
        {
            status = StatusCodes.Status500InternalServerError;
            logger.UnhandledException(exception, httpContext.Request.Method, ApiRequests.RouteTemplate(httpContext));
        }

        httpContext.Response.StatusCode = status;
        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = { Status = status },
        }).ConfigureAwait(false);
    }
}
