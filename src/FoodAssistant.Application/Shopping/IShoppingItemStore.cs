namespace FoodAssistant.Application.Shopping;

/// <summary>
/// Shopping list entries of the active household (household query filter applies everywhere).
/// <see cref="ShoppingItemRecord.Version"/> is a counter the application increments on every
/// change; the store compares it on save (ADR-0005, docs/kb/datenmodell.md).
/// </summary>
public interface IShoppingItemStore
{
    /// <summary>Entries of a list without tombstones, in creation order.</summary>
    Task<IReadOnlyList<ShoppingItemRecord>> ListItemsAsync(Guid listId, bool includeChecked, CancellationToken cancellationToken);

    /// <param name="itemId">Entry id.</param>
    /// <param name="includeDeleted">Also return tombstones (the conflict rules need them).</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    Task<ShoppingItemRecord?> GetItemAsync(Guid itemId, bool includeDeleted, CancellationToken cancellationToken);

    /// <summary>
    /// Inserts the entry with version 1 together with pending activity entries. <see langword="null"/>
    /// if the id is already taken (a concurrent insert or another household) or the list vanished
    /// meanwhile; nothing is saved then and pending changes are discarded.
    /// </summary>
    Task<ShoppingItemRecord?> AddItemAsync(
        Guid listId,
        NewShoppingItem item,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    /// <summary>
    /// Stores all mutable fields of <paramref name="updated"/> together with pending activity entries,
    /// if the stored version still is <paramref name="expectedVersion"/>. <see langword="false"/>
    /// when the entry changed or vanished meanwhile; nothing is saved then and pending changes are
    /// discarded, so the caller can start over with fresh state.
    /// </summary>
    Task<bool> ApplyItemAsync(ShoppingItemRecord updated, long expectedVersion, CancellationToken cancellationToken);

    /// <summary>Saves pending activity entries when no entry changed (a lost conflict is still recorded).</summary>
    Task SavePendingAsync(CancellationToken cancellationToken);

    /// <summary>Name, category and section of an active (not archived) product, or <see langword="null"/>.</summary>
    Task<(string Name, string CategoryKey, string? SectionKey)?> GetProductForItemAsync(
        Guid productId,
        CancellationToken cancellationToken);
}
