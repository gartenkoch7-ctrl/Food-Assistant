using System.Text.Json.Serialization;

namespace FoodAssistant.Contracts.Inventory;

/// <summary>State of a batch (docs/kb/vorrat-und-haltbarkeit.md). Mirrors the domain enum; serialized as lower-case string.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<BatchStateDto>))]
public enum BatchStateDto
{
    /// <summary>Unopened package.</summary>
    [JsonStringEnumMemberName("sealed")]
    Sealed = 0,

    /// <summary>Opened package.</summary>
    [JsonStringEnumMemberName("opened")]
    Opened = 1,

    /// <summary>Frozen by the household.</summary>
    [JsonStringEnumMemberName("frozen")]
    Frozen = 2,

    /// <summary>Thawed after freezing.</summary>
    [JsonStringEnumMemberName("thawed")]
    Thawed = 3,

    /// <summary>Leftover or home-made.</summary>
    [JsonStringEnumMemberName("leftover")]
    Leftover = 4,
}
