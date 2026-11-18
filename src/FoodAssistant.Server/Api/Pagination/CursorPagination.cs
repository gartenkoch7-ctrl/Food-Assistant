using System.Buffers.Text;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using FoodAssistant.Contracts.Common;
using FoodAssistant.Server.Api.Errors;
using Microsoft.AspNetCore.Http.HttpResults;

namespace FoodAssistant.Server.Api.Pagination;

/// <summary>
/// Cursor pagination (ADR-0004: <c>?limit=50&amp;cursor=…</c>, answer with <c>nextCursor</c>).
/// A cursor is the Base64url-encoded JSON of the sort position of the last item, e.g.
/// <c>record Position(DateOnly ExpiresOn, Guid Id)</c>. Clients treat it as opaque.
/// </summary>
/// <remarks>
/// Cursors are not signed: they only carry a sort position, and every query still filters by
/// household and permissions, so a forged cursor cannot reveal anything.
/// </remarks>
public static class CursorPagination
{
    /// <summary>Page size when the client sends no <c>limit</c>.</summary>
    public const int DefaultLimit = 50;

    /// <summary>Largest page size; larger requests are capped.</summary>
    public const int MaxLimit = 200;

    // Positions are a few ids and dates; anything longer is not one of our cursors.
    private const int MaxCursorLength = 1024;

    // Strict: a truncated or forged cursor ("{}") must fail to decode instead of yielding null fields.
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        RespectRequiredConstructorParameters = true,
        RespectNullableAnnotations = true,
    };

    /// <summary>Applies the default and the upper bound to a requested page size.</summary>
    public static int NormalizeLimit(int? limit) =>
        limit is null or < 1 ? DefaultLimit : Math.Min(limit.Value, MaxLimit);

    /// <summary>Encodes the sort position of the last item of a page.</summary>
    public static string Encode<TPosition>(TPosition position) =>
        Base64Url.EncodeToString(JsonSerializer.SerializeToUtf8Bytes(position, SerializerOptions));

    /// <summary>Decodes a cursor from the query; <c>false</c> for anything malformed.</summary>
    public static bool TryDecode<TPosition>(string? cursor, [NotNullWhen(true)] out TPosition? position)
    {
        position = default;
        if (string.IsNullOrEmpty(cursor) || cursor.Length > MaxCursorLength)
        {
            return false;
        }

        try
        {
            position = JsonSerializer.Deserialize<TPosition>(Base64Url.DecodeFromChars(cursor), SerializerOptions);
            return position is not null;
        }
        catch (FormatException)
        {
            return false;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    /// <summary>400 <c>common.cursor_invalid</c> for a cursor that <see cref="TryDecode{TPosition}"/> rejected.</summary>
    public static ProblemHttpResult InvalidCursor() =>
        ApiProblems.BadRequest(ErrorCodes.CursorInvalid, "The cursor is invalid. Start again from the first page.");

    /// <summary>
    /// Builds a page from <paramref name="items"/> queried with <c>limit + 1</c>: the extra item
    /// only signals that another page exists and is not returned.
    /// </summary>
    public static CursorPage<T> ToPage<T, TPosition>(IReadOnlyList<T> items, int limit, Func<T, TPosition> positionOf)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(positionOf);
        ArgumentOutOfRangeException.ThrowIfLessThan(limit, 1); // Pass the result of NormalizeLimit.
        if (items.Count <= limit)
        {
            return new CursorPage<T>(items, NextCursor: null);
        }

        var page = items.Take(limit).ToList();
        return new CursorPage<T>(page, Encode(positionOf(page[^1])));
    }
}
