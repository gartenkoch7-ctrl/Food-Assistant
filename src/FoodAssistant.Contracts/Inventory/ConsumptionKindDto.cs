using System.Text.Json.Serialization;

namespace FoodAssistant.Contracts.Inventory;

/// <summary>Kind of a consumption event. Mirrors the domain enum; serialized as snake_case string.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ConsumptionKindDto>))]
public enum ConsumptionKindDto
{
    /// <summary>The whole batch was used.</summary>
    [JsonStringEnumMemberName("used_up")]
    UsedUp = 0,

    /// <summary>Part of the batch was used.</summary>
    [JsonStringEnumMemberName("partial")]
    Partial = 1,

    /// <summary>Thrown away; comes with a reason.</summary>
    [JsonStringEnumMemberName("discarded")]
    Discarded = 2,
}
