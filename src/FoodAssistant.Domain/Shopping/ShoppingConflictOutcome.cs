namespace FoodAssistant.Domain.Shopping;

/// <summary>Which rule of ADR-0005 decided a <see cref="ShoppingResolution"/>; input for the activity log.</summary>
public enum ShoppingConflictOutcome
{
    /// <summary>The server did not know the item; it is added (two new entries are both kept).</summary>
    Created,

    /// <summary>The client knew the current version; its change applies as is.</summary>
    NoConflict,

    /// <summary>Checked vs. any change: the checked side wins.</summary>
    CheckedWins,

    /// <summary>Deleted vs. changed: the change wins and the item stays.</summary>
    ChangeWinsOverDelete,

    /// <summary>Deleted vs. checked: the item stays checked.</summary>
    CheckedWinsOverDelete,

    /// <summary>Both sides deleted the item; it stays deleted.</summary>
    BothDeleted,

    /// <summary>Quantity/note changed on both sides: the change received last by the server wins.</summary>
    LastWriteWins,
}
