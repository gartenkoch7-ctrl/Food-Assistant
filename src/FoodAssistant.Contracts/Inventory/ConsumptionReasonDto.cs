using System.Text.Json.Serialization;

namespace FoodAssistant.Contracts.Inventory;

/// <summary>Reason for discarding food. Mirrors the domain enum; serialized as snake_case string.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ConsumptionReasonDto>))]
public enum ConsumptionReasonDto
{
    /// <summary>Past its date.</summary>
    [JsonStringEnumMemberName("expired")]
    Expired = 0,

    /// <summary>Spoiled.</summary>
    [JsonStringEnumMemberName("spoiled")]
    Spoiled = 1,

    /// <summary>Did not taste good.</summary>
    [JsonStringEnumMemberName("disliked_taste")]
    DislikedTaste = 2,

    /// <summary>Too much was cooked.</summary>
    [JsonStringEnumMemberName("too_much_cooked")]
    TooMuchCooked = 3,

    /// <summary>Any other reason.</summary>
    [JsonStringEnumMemberName("other")]
    Other = 4,
}
