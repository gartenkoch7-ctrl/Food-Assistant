namespace FoodAssistant.Application.Shopping;

/// <summary>Kind keys of shopping entries in the activity feed (<c>ActivityArea.Shopping</c>).</summary>
public static class ShoppingActivityKinds
{
    public const string ItemAdded = "item_added";

    public const string ItemChecked = "item_checked";

    public const string ItemDeleted = "item_deleted";

    /// <summary>A conflict was resolved by a rule other than "created" or "no conflict"; payload <c>{"outcome":"…"}</c>.</summary>
    public const string ConflictResolved = "conflict_resolved";
}
