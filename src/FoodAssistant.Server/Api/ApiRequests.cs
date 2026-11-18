using System.Net;
using System.Net.Sockets;
using System.Security.Claims;
using Microsoft.AspNetCore.Diagnostics;

namespace FoodAssistant.Server.Api;

/// <summary>Request facts the API conventions share: which requests count as API, who is calling, which route matched.</summary>
internal static class ApiRequests
{
    private static readonly PathString ApiRoot = new("/api");
    private static readonly PathString ApiV1Root = new("/api/v1");

    /// <summary>API requests get ProblemDetails for every error, never HTML error pages.</summary>
    public static bool IsApi(HttpContext context) => context.Request.Path.StartsWithSegments(ApiRoot);

    public static bool IsApiV1Write(HttpContext context) =>
        context.Request.Path.StartsWithSegments(ApiV1Root) && IsWriteMethod(context.Request.Method);

    /// <summary>Methods that change data and may carry an <c>Idempotency-Key</c>.</summary>
    public static bool IsWriteMethod(string method) =>
        HttpMethods.IsPost(method) || HttpMethods.IsPut(method) || HttpMethods.IsPatch(method) || HttpMethods.IsDelete(method);

    /// <summary>
    /// Stable id of the signed-in person, or <c>null</c> when anonymous. Accepts the ASP.NET Core
    /// name-identifier claim and the OIDC <c>sub</c> claim, whichever the auth setup maps.
    /// </summary>
    public static string? PersonId(ClaimsPrincipal user) =>
        user.Identity?.IsAuthenticated == true
            ? user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub")
            : null;

    /// <summary>
    /// Client network as a key: the IPv4 address, or the /64 prefix of an IPv6 address (one
    /// connection usually owns a whole /64, so per-address keys are trivial to rotate). The
    /// address is already rewritten from <c>X-Forwarded-For</c> for trusted proxies.
    /// </summary>
    public static string ClientAddressKey(HttpContext context)
    {
        var address = context.Connection.RemoteIpAddress;
        if (address is null)
        {
            return "ip:unknown";
        }

        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        if (address.AddressFamily != AddressFamily.InterNetworkV6)
        {
            return "ip:" + address;
        }

        var bytes = address.GetAddressBytes();
        Array.Clear(bytes, 8, 8);
        return "ip:" + new IPAddress(bytes) + "/64";
    }

    /// <summary><c>person:&lt;id&gt;</c> of the signed-in person, or <c>null</c> when anonymous.</summary>
    public static string? PersonKey(ClaimsPrincipal user) =>
        PersonId(user) is { } personId ? "person:" + personId : null;

    /// <summary>Who is calling: the person when signed in, otherwise the client network.</summary>
    public static string CallerKey(HttpContext context) =>
        PersonKey(context.User) ?? ClientAddressKey(context);

    /// <summary>Route template for logs and metrics; never the concrete path, which may contain ids or user input.</summary>
    public static string RouteTemplate(HttpContext context)
    {
        // The exception handler clears the endpoint before handlers run; its feature keeps it.
        var endpoint = context.GetEndpoint() ?? context.Features.Get<IExceptionHandlerFeature>()?.Endpoint;
        return (endpoint as RouteEndpoint)?.RoutePattern.RawText ?? "(unmatched)";
    }
}
