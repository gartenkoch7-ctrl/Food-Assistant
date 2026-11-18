using System.Globalization;
using FoodAssistant.Contracts.Common;
using FoodAssistant.Server.Api.Errors;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;

namespace FoodAssistant.Server.Api.Concurrency;

/// <summary>
/// Optimistic concurrency (ADR-0004): every changeable resource has a version (PostgreSQL
/// <c>xmin</c> or a version column) that is sent as a strong <c>ETag</c>. Changes send it back in
/// <c>If-Match</c>; a mismatch answers 412 with the current ETag, a missing header 428.
/// </summary>
/// <example>
/// <code>
/// var item = await repository.GetAsync(id, ct);
/// if (ETags.CheckIfMatch(httpContext, item.Version) is { } conflict) return conflict;
/// // ... apply the change, then:
/// httpContext.Response.SetETag(updated.Version);
/// </code>
/// </example>
public static class ETags
{
    /// <summary>Extension field of the 412 problem with the current ETag.</summary>
    public const string CurrentETagField = "currentETag";

    /// <summary>Extension field of the 412 problem with the current representation, if the endpoint passes one.</summary>
    public const string CurrentField = "current";

    /// <summary>Strong ETag for a version, e.g. <c>"42"</c>.</summary>
    public static string FromVersion(long version) =>
        $"\"{version.ToString(CultureInfo.InvariantCulture)}\"";

    /// <summary>Sets the <c>ETag</c> response header; call it on every read and write of a versioned resource.</summary>
    public static void SetETag(this HttpResponse response, long version)
    {
        ArgumentNullException.ThrowIfNull(response);
        response.Headers.ETag = FromVersion(version);
    }

    /// <summary>
    /// Returns <c>null</c> when the change may proceed, otherwise the problem to return: 428
    /// <c>common.precondition_required</c> when <c>If-Match</c> is missing (and required), 400
    /// <c>common.bad_request</c> when it is malformed, 412 <c>common.precondition_failed</c> with
    /// the current <c>ETag</c> header when it does not match.
    /// </summary>
    /// <param name="httpContext">Current request.</param>
    /// <param name="currentVersion">Stored version of the resource.</param>
    /// <param name="current">Optional current representation (DTO), included as <c>current</c> so the client can merge.</param>
    /// <param name="required">
    /// ADR-0004: changes send <c>If-Match</c>, so a missing header is rejected; without it a stale
    /// client would silently overwrite someone else's change. Pass <c>false</c> only where a
    /// blind overwrite is intended.
    /// </param>
    public static ProblemHttpResult? CheckIfMatch(HttpContext httpContext, long currentVersion, object? current = null, bool required = true)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        var header = httpContext.Request.Headers.IfMatch;
        if (header.Count == 0)
        {
            return required
                ? ApiProblems.Problem(
                    StatusCodes.Status428PreconditionRequired,
                    ErrorCodes.PreconditionRequired,
                    "Send the ETag of the version you changed in the If-Match header.")
                : null;
        }

        if (!TryParseIfMatch(header, out var tags))
        {
            // Empty or unquoted (If-Match: 42): a client bug, not a conflict; 412 would make it reload forever.
            return ApiProblems.BadRequest(detail: "If-Match must contain quoted ETags, e.g. \"42\", or *.");
        }

        return Matches(tags, currentVersion) ? null : PreconditionFailed(httpContext, currentVersion, current);
    }

    /// <summary>
    /// 412 <c>common.precondition_failed</c> with the current ETag as header and in the body (and
    /// the current representation). Also for a version conflict found only while saving.
    /// </summary>
    public static ProblemHttpResult PreconditionFailed(HttpContext httpContext, long currentVersion, object? current = null)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        httpContext.Response.SetETag(currentVersion);
        var extensions = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [CurrentETagField] = FromVersion(currentVersion),
        };
        if (current is not null)
        {
            extensions[CurrentField] = current;
        }

        return ApiProblems.Problem(
            StatusCodes.Status412PreconditionFailed,
            ErrorCodes.PreconditionFailed,
            "The resource was changed in the meantime. Reload it and apply the change again.",
            extensions);
    }

    // Strict: one malformed entry ("42, \"3\"") makes the whole header invalid instead of being skipped.
    private static bool TryParseIfMatch(StringValues header, out IList<EntityTagHeaderValue> tags) =>
        EntityTagHeaderValue.TryParseStrictList(header, out tags!) && tags.Count > 0;

    // Weak comparison on purpose (RFC 9110 asks for strong): compressing proxies (nginx gzip) turn
    // "42" into W/"42". The tag is the version number, which identifies the stored state
    // regardless of the transfer encoding.
    private static bool Matches(IList<EntityTagHeaderValue> tags, long currentVersion)
    {
        var current = new EntityTagHeaderValue(FromVersion(currentVersion));
        return tags.Any(tag => tag.Equals(EntityTagHeaderValue.Any) || tag.Compare(current, useStrongComparison: false));
    }
}
