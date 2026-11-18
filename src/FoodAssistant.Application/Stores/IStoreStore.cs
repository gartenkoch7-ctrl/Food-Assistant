using FoodAssistant.Application.Common;
using FoodAssistant.Contracts.Stores;

namespace FoodAssistant.Application.Stores;

/// <summary>Stores of the active household (household query filter applies everywhere).</summary>
public interface IStoreStore
{
    /// <summary>All stores by name.</summary>
    Task<IReadOnlyList<StoreDto>> ListAsync(CancellationToken cancellationToken);

    Task<bool> ExistsAsync(Guid storeId, CancellationToken cancellationToken);

    /// <summary>Adds a store; <c>Conflict(common.conflict)</c> if the id is taken.</summary>
    Task<OperationResult<StoreDto>> AddAsync(Guid id, string name, DateTimeOffset now, CancellationToken cancellationToken);

    /// <summary>Renames a store (store pattern, docs/kb/datenmodell.md).</summary>
    Task<OperationResult<StoreDto>> UpdateAsync(
        Guid storeId,
        long? expectedVersion,
        string name,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    /// <summary>
    /// Deletes a store with its section order data; lists using it lose the store through the
    /// context (not the database cascade), so the change log records them.
    /// </summary>
    Task<OperationResult<StoreDto>> DeleteAsync(
        Guid storeId,
        long? expectedVersion,
        DateTimeOffset now,
        CancellationToken cancellationToken);
}
