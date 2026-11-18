using System.Text.Json.Serialization;

namespace FoodAssistant.Contracts.Households;

/// <summary>Lifecycle state of an invitation. Serialized as lower-case string.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<InvitationStatus>))]
public enum InvitationStatus
{
    [JsonStringEnumMemberName("pending")]
    Pending = 0,

    [JsonStringEnumMemberName("accepted")]
    Accepted = 1,

    [JsonStringEnumMemberName("revoked")]
    Revoked = 2,

    [JsonStringEnumMemberName("expired")]
    Expired = 3,
}
