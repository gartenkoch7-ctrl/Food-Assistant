namespace FoodAssistant.Domain.Expiry;

/// <summary>Status of an expiry date relative to today (docs/kb/vorrat-und-haltbarkeit.md).</summary>
public enum ExpiryStatus
{
    /// <summary>The date is before today.</summary>
    Expired,

    /// <summary>today &lt;= date &lt;= today + lead days.</summary>
    Soon,

    /// <summary>The date is after today + lead days.</summary>
    Ok,

    /// <summary>No date.</summary>
    NoDate,
}
