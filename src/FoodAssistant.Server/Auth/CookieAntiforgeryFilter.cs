using FoodAssistant.Contracts.Accounts;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;

namespace FoodAssistant.Server.Auth;

/// <summary>
/// CSRF protection for cookie-authenticated writes (ADR-0003). Bearer requests need none: a
/// foreign site cannot set the Authorization header. JSON endpoints are not covered by the
/// framework's antiforgery middleware, hence this filter.
/// </summary>
/// <param name="always">Also check anonymous requests (login: prevents login CSRF).</param>
internal sealed class CookieAntiforgeryFilter(bool always) : IEndpointFilter
{
    /// <summary>Header that carries the request token (see <c>GET /api/v1/auth/antiforgery</c>).</summary>
    public const string HeaderName = "X-XSRF-TOKEN";

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        var httpContext = context.HttpContext;
        if (IsUnsafe(httpContext.Request.Method) && (always || IsCookieAuthenticated(httpContext)))
        {
            var antiforgery = httpContext.RequestServices.GetRequiredService<IAntiforgery>();
            if (!await antiforgery.IsRequestValidAsync(httpContext).ConfigureAwait(false))
            {
                var logger = httpContext.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger(AuthLog.Category);
                AuthLog.AntiforgeryRejected(logger);
                return ProblemResults.Problem(
                    StatusCodes.Status400BadRequest,
                    AccountErrorCodes.AntiforgeryInvalid,
                    "The antiforgery token is missing or invalid.");
            }
        }

        return await next(context).ConfigureAwait(false);
    }

    private static bool IsUnsafe(string method) =>
        !(HttpMethods.IsGet(method) || HttpMethods.IsHead(method) || HttpMethods.IsOptions(method) || HttpMethods.IsTrace(method));

    private static bool IsCookieAuthenticated(HttpContext httpContext) =>
        httpContext.User.Identities.Any(i =>
            i.IsAuthenticated && string.Equals(i.AuthenticationType, IdentityConstants.ApplicationScheme, StringComparison.Ordinal));
}
