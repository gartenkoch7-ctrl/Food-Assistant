using System.Text.Json.Serialization;

namespace FoodAssistant.Contracts.Restock;

/// <summary>How often a recurring rule adds its entry. Mirrors the domain enum; serialized as snake_case string.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<RecurrenceFrequencyDto>))]
public enum RecurrenceFrequencyDto
{
    /// <summary>Every day.</summary>
    [JsonStringEnumMemberName("daily")]
    Daily = 0,

    /// <summary>Every week on <c>dayOfWeek</c>.</summary>
    [JsonStringEnumMemberName("weekly")]
    Weekly = 1,

    /// <summary>Every month on <c>dayOfMonth</c>.</summary>
    [JsonStringEnumMemberName("monthly")]
    Monthly = 2,
}
