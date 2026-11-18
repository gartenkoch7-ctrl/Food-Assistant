namespace FoodAssistant.Contracts.Sync;

/// <summary>Stable entity type names in the change log and in live updates (ADR-0005).</summary>
public static class SyncEntityTypes
{
    public const string StorageLocation = "storage_location";
    public const string Product = "product";
    public const string Batch = "batch";
    public const string ShoppingList = "shopping_list";
    public const string ShoppingItem = "shopping_item";
    public const string Store = "store";
}
