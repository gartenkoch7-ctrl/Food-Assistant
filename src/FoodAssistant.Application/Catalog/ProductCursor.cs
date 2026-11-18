using System.Buffers.Text;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace FoodAssistant.Application.Catalog;

/// <summary>
/// Sort position of the last product of a page (<c>Name</c>, <c>Id</c>) and its opaque wire format
/// (Base64url JSON, like <c>CursorPagination</c> in the server, which Application cannot reference).
/// </summary>
public sealed record ProductCursor(string Name, Guid Id)
{
    // Same bound as the server's cursors: a position is one name and one id.
    private const int MaxLength = 1024;

    // Strict: a truncated or forged cursor ("{}") must fail instead of yielding null fields.
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        RespectRequiredConstructorParameters = true,
        RespectNullableAnnotations = true,
    };

    public string Encode() => Base64Url.EncodeToString(JsonSerializer.SerializeToUtf8Bytes(this, Options));

    /// <summary><see langword="false"/> for anything that is not a cursor produced by <see cref="Encode"/>.</summary>
    public static bool TryDecode(string? cursor, [NotNullWhen(true)] out ProductCursor? position)
    {
        position = null;
        if (string.IsNullOrEmpty(cursor) || cursor.Length > MaxLength)
        {
            return false;
        }

        try
        {
            position = JsonSerializer.Deserialize<ProductCursor>(Base64Url.DecodeFromChars(cursor), Options);
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
}
