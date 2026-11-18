using System.Text.Json.Serialization;

namespace FoodAssistant.Contracts.Households;

/// <summary>Role of a person inside one household (ADR-0002). Serialized as lower-case string.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<HouseholdRole>))]
public enum HouseholdRole
{
    /// <summary>View inventory and shopping list, check off and add shopping items.</summary>
    [JsonStringEnumMemberName("restricted")]
    Restricted = 0,

    /// <summary>Everything except member and household administration.</summary>
    [JsonStringEnumMemberName("member")]
    Member = 1,

    /// <summary>Everything in the household, including invitations and roles.</summary>
    [JsonStringEnumMemberName("owner")]
    Owner = 2,
}
