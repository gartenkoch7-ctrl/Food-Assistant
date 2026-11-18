using System.Text.Json.Serialization;

namespace FoodAssistant.Contracts.Inventory;

/// <summary>Type of a storage location (ADR-0010). Mirrors the domain enum; serialized as lower-case string.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<StorageTypeDto>))]
public enum StorageTypeDto
{
    /// <summary>Fridge.</summary>
    [JsonStringEnumMemberName("chilled")]
    Chilled = 0,

    /// <summary>Freezer.</summary>
    [JsonStringEnumMemberName("frozen")]
    Frozen = 1,

    /// <summary>Pantry, room temperature.</summary>
    [JsonStringEnumMemberName("ambient")]
    Ambient = 2,
}
