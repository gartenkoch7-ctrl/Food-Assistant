namespace FoodAssistant.Contracts.Inventory;

/// <summary>Stable <c>errorCode</c> values of inventory endpoints (ADR-0004). The UI translates these.</summary>
public static class InventoryErrorCodes
{
    /// <summary>Unknown batch or one of another household (404).</summary>
    public const string BatchNotFound = "inventory.batch_not_found";

    /// <summary>Unknown activity or one of another household (404).</summary>
    public const string ActivityNotFound = "inventory.activity_not_found";

    /// <summary>A batch with the requested id already exists (409).</summary>
    public const string BatchExists = "inventory.batch_exists";

    /// <summary>Request data is invalid; see <c>errors</c> (400).</summary>
    public const string ValidationFailed = "inventory.validation_failed";

    /// <summary>The batch is not in a state that allows the action, e.g. opening an opened batch (422).</summary>
    public const string InvalidState = "inventory.invalid_state";

    /// <summary>Thawed food must not be frozen again (422).</summary>
    public const string RefreezeNotAllowed = "inventory.refreeze_not_allowed";

    /// <summary>Date and kind are only correctable while the batch is sealed (422).</summary>
    public const string DateLocked = "inventory.date_locked";

    /// <summary>The batches of the product do not cover the requested amount (422).</summary>
    public const string NotEnoughStock = "inventory.not_enough_stock";

    /// <summary>The action is older than 24 hours, already undone, or the batch changed since (409).</summary>
    public const string UndoNotPossible = "inventory.undo_not_possible";

    /// <summary>The unit does not convert into the unit of the batch (422).</summary>
    public const string UnitMismatch = "inventory.unit_mismatch";
}
