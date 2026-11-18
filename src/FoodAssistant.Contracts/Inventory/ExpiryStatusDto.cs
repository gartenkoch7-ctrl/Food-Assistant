namespace FoodAssistant.Contracts.Inventory;

/// <summary>
/// Expiry status of an inventory item as shown to the user (docs/kb/vorrat-und-haltbarkeit.md, "Status und Formulierungen").
/// The server derives it; clients only display it.
/// </summary>
public enum ExpiryStatusDto
{
    /// <summary>No date is known for the item.</summary>
    NoDate = 0,

    /// <summary>The date has passed.</summary>
    Expired = 1,

    /// <summary>The date is today or within the household's look-ahead window.</summary>
    Soon = 2,

    /// <summary>The date lies beyond the look-ahead window.</summary>
    Ok = 3,
}
