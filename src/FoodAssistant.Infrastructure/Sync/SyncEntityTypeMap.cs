using FoodAssistant.Contracts.Sync;
using FoodAssistant.Infrastructure.Catalog;
using FoodAssistant.Infrastructure.Inventory;
using FoodAssistant.Infrastructure.Persistence;
using FoodAssistant.Infrastructure.Shopping;
using FoodAssistant.Infrastructure.Stores;

namespace FoodAssistant.Infrastructure.Sync;

/// <summary>
/// Stable change-log name of every <see cref="ISyncTracked"/> entity (<see cref="SyncEntityTypes"/>).
/// A new synchronised entity needs a row here; a test checks that every one in the model has one.
/// </summary>
internal static class SyncEntityTypeMap
{
    private static readonly Dictionary<Type, string> s_keys = new()
    {
        [typeof(StorageLocation)] = SyncEntityTypes.StorageLocation,
        [typeof(Product)] = SyncEntityTypes.Product,
        [typeof(StockBatchRecord)] = SyncEntityTypes.Batch,
        [typeof(ShoppingList)] = SyncEntityTypes.ShoppingList,
        [typeof(ShoppingItem)] = SyncEntityTypes.ShoppingItem,
        [typeof(Store)] = SyncEntityTypes.Store,
    };

    /// <exception cref="InvalidOperationException">The type has no sync entity type.</exception>
    public static string KeyOf(Type entityType)
    {
        ArgumentNullException.ThrowIfNull(entityType);
        return s_keys.TryGetValue(entityType, out var key)
            ? key
            : throw new InvalidOperationException(
                $"{entityType.Name} is synchronised but has no sync entity type; add it to {nameof(SyncEntityTypeMap)}.");
    }
}
