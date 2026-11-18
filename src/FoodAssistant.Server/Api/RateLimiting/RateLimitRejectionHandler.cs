using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace FoodAssistant.Server.Api.RateLimiting;

/// <summary>Answers a rejected request with 429 ProblemDetails <c>common.rate_limited</c> and <c>Retry-After</c>.</summary>
internal sealed class RateLimitRejectionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<RateLimitRejectionHandler> logger)
{
    public async ValueTask HandleAsync(OnRejectedContext context, CancellationToken cancellationToken)
    {
        var httpContext = context.HttpContext;
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            httpContext.Response.Headers.RetryAfter =
                ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
        }

        var policy = httpContext.GetEndpoint()?.Metadata.GetMetadata<EnableRateLimitingAttribute>()?.PolicyName
            ?? (ApiRequests.IsOpenId(httpContext) ? "openid" : "global");
        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.Rejected(policy, httpContext.Request.Method, ApiRequests.RouteTemplate(httpContext));
        }

        ApiMetrics.RateLimitRejected(policy);

        // Also for non-API routes (sign-in pages): there the default writer answers JSON clients.
        // errorCode common.rate_limited comes from the status (ProblemDetailsEnricher).
        await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails =
            {
                Status = StatusCodes.Status429TooManyRequests,
                Detail = "Too many requests. Try again later.",
            },
        }).ConfigureAwait(false);
    }
}
