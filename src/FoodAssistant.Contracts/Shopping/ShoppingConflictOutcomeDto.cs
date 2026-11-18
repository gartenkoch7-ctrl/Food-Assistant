using System.Text.Json.Serialization;

namespace FoodAssistant.Contracts.Shopping;

/// <summary>Which conflict rule of ADR-0005 decided a change. Mirrors the domain enum; serialized as snake_case string.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ShoppingConflictOutcomeDto>))]
public enum ShoppingConflictOutcomeDto
{
    /// <summary>The entry was new and is added.</summary>
    [JsonStringEnumMemberName("created")]
    Created = 0,

    /// <summary>The change was based on the current version and applies as is.</summary>
    [JsonStringEnumMemberName("no_conflict")]
    NoConflict = 1,

    /// <summary>Checked vs. any change: the checked side wins.</summary>
    [JsonStringEnumMemberName("checked_wins")]
    CheckedWins = 2,

    /// <summary>Deleted vs. changed: the change wins and the entry stays.</summary>
    [JsonStringEnumMemberName("change_wins_over_delete")]
    ChangeWinsOverDelete = 3,

    /// <summary>Deleted vs. checked: the entry stays checked.</summary>
    [JsonStringEnumMemberName("checked_wins_over_delete")]
    CheckedWinsOverDelete = 4,

    /// <summary>Both sides deleted the entry; it stays deleted.</summary>
    [JsonStringEnumMemberName("both_deleted")]
    BothDeleted = 5,

    /// <summary>Changed on both sides: the change the server received last wins.</summary>
    [JsonStringEnumMemberName("last_write_wins")]
    LastWriteWins = 6,
}
