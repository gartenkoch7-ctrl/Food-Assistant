using System.Text.Json.Serialization;

namespace FoodAssistant.Contracts.Inventory;

/// <summary>Kind of an expiry date (glossary: MHD, Verbrauchsdatum). Mirrors the domain enum; serialized as snake_case string.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ExpiryKindDto>))]
public enum ExpiryKindDto
{
    /// <summary>No expiry date.</summary>
    [JsonStringEnumMemberName("none")]
    None = 0,

    /// <summary>Best before ("MHD"): often still fine afterwards.</summary>
    [JsonStringEnumMemberName("best_before")]
    BestBefore = 1,

    /// <summary>Use by ("Verbrauchsdatum"): must not be eaten afterwards.</summary>
    [JsonStringEnumMemberName("use_by")]
    UseBy = 2,
}
