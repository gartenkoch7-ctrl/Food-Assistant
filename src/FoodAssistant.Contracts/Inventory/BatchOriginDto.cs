using System.Text.Json.Serialization;

namespace FoodAssistant.Contracts.Inventory;

/// <summary>How a batch came into the inventory. Mirrors the domain enum; serialized as lower-case string.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<BatchOriginDto>))]
public enum BatchOriginDto
{
    /// <summary>Entered by hand.</summary>
    [JsonStringEnumMemberName("manual")]
    Manual = 0,

    /// <summary>Added after scanning a barcode.</summary>
    [JsonStringEnumMemberName("scanned")]
    Scanned = 1,

    /// <summary>Taken over from a shopping trip.</summary>
    [JsonStringEnumMemberName("purchase")]
    Purchase = 2,

    /// <summary>Imported.</summary>
    [JsonStringEnumMemberName("import")]
    Import = 3,

    /// <summary>Leftover or home-made dish.</summary>
    [JsonStringEnumMemberName("leftover")]
    Leftover = 4,
}
