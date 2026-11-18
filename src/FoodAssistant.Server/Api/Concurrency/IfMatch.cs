using System.Globalization;
using Microsoft.Net.Http.Headers;

namespace FoodAssistant.Server.Api.Concurrency;

/// <summary>
/// Reads the expected version from <c>If-Match</c> for use cases that compare it themselves
/// (stores, see docs/kb/datenmodell.md). Unlike <see cref="ETags.CheckIfMatch"/> it does not
/// need the stored version up front.
/// </summary>
/// <example>
/// <code>
/// if (!IfMatch.TryRead(http, required: true, out var expected, out var problem)) return problem!;
/// var result = await service.UpdateAsync(householdId, id, request, expected, ct);
/// return result.ToHttpResult(http, p => p.Version);
/// </code>
/// </example>
public static class IfMatch
{
    /// <summary>
    /// <see langword="true"/> with <paramref name="expectedVersion"/> = the version of the single ETag
    /// (strong or weak), or <see langword="null"/> for <c>*</c> or a missing optional header.
    /// <see langword="false"/> with <paramref name="problem"/>: 428 when the header is missing but
    /// <paramref name="required"/>, 400 when it is malformed, lists several tags or no version number.
    /// </summary>
    public static bool TryRead(HttpContext http, bool required, out long? expectedVersion, out IResult? problem)
    {
        ArgumentNullException.ThrowIfNull(http);
        expectedVersion = null;
        problem = null;

        var header = http.Request.Headers.IfMatch;
        if (header.Count == 0)
        {
            problem = required ? ETags.PreconditionRequired() : null;
            return !required;
        }

        // Several tags make no sense for one stored version; reject instead of guessing which one counts.
        if (!ETags.TryParseIfMatch(header, out var tags) || tags.Count != 1)
        {
            problem = ETags.MalformedIfMatch();
            return false;
        }

        var tag = tags[0];
        if (tag.Equals(EntityTagHeaderValue.Any))
        {
            return true;
        }

        // The tag includes its quotes ("42"); weak tags (W/"42") count like strong ones, see ETags.Matches.
        var value = tag.Tag.Subsegment(1, tag.Tag.Length - 2);
        if (!long.TryParse(value.AsSpan(), NumberStyles.None, CultureInfo.InvariantCulture, out var version))
        {
            problem = ETags.MalformedIfMatch();
            return false;
        }

        expectedVersion = version;
        return true;
    }
}
