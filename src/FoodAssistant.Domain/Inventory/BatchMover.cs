namespace FoodAssistant.Domain.Inventory;

/// <summary>
/// Moves a batch to a storage location of another type: into the freezer freezes it, out of the
/// freezer thaws it (ADR-0010: the storage type of the location decides).
/// </summary>
public static class BatchMover
{
    /// <summary>Applies the move and returns what happened.</summary>
    /// <param name="batch">Batch to move.</param>
    /// <param name="target">Storage type of the target location.</param>
    /// <param name="today">Household's calendar day.</param>
    /// <param name="rule">
    /// Resolved rule: for freezing the row (category, <see cref="StorageType.Frozen"/>), for thawing
    /// the row (category, <paramref name="target"/>); unused otherwise.
    /// </param>
    /// <exception cref="InvalidOperationException">A thawed batch is moved into the freezer.</exception>
    public static BatchMove Move(StockBatch batch, StorageType target, DateOnly today, ShelfLifeRule rule)
    {
        ArgumentNullException.ThrowIfNull(batch);
        ArgumentNullException.ThrowIfNull(rule);
        if (batch.StorageType == target)
        {
            return BatchMove.SameType;
        }

        if (target == StorageType.Frozen)
        {
            batch.Freeze(today, rule);
            return BatchMove.Frozen;
        }

        if (batch.StorageType == StorageType.Frozen)
        {
            batch.Thaw(today, rule, target);
            return BatchMove.Thawed;
        }

        batch.Relocate(target);
        return BatchMove.Relocated;
    }
}
