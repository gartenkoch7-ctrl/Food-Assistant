namespace FoodAssistant.Application.Inventory;

/// <summary>Kind keys of inventory entries in the activity feed (stable; the feed translates them).</summary>
public static class InventoryActivityKinds
{
    public const string BatchAdded = "batch_added";
    public const string BatchCorrected = "batch_corrected";
    public const string BatchMoved = "batch_moved";
    public const string BatchFrozen = "batch_frozen";
    public const string BatchThawed = "batch_thawed";
    public const string BatchOpened = "batch_opened";
    public const string LeftoverCreated = "leftover_created";
    public const string UsedUp = "used_up";
    public const string ConsumedPartly = "consumed_partly";
    public const string Discarded = "discarded";
    public const string Undone = "undone";
}
