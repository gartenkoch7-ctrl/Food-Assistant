using FoodAssistant.Application.Common;

namespace FoodAssistant.Application.Checkout;

/// <summary>Reads and writes for completing a shopping list (active household only).</summary>
public interface ICheckoutStore
{
    /// <summary>
    /// The checked entries of a list with their products, plus <paramref name="extraProductIds"/>
    /// (products the client assigned), and all storage locations.
    /// </summary>
    /// <returns>Null if the list does not exist in the active household.</returns>
    Task<CheckoutData?> LoadAsync(Guid listId, IReadOnlyCollection<Guid> extraProductIds, CancellationToken cancellationToken);

    /// <summary>
    /// Writes new products, batches, product memories, feed entries and removes the entries, all in
    /// one transaction. Under the household's change-log lock every entry is checked again (still on
    /// the list, checked, not deleted, same version) and every existing product (not archived); lines
    /// that fail are left out and reported as stale instead of failing the whole request.
    /// </summary>
    /// <returns>
    /// The stored lines; 404 <c>checkout.list_not_found</c> if the list is gone, 409
    /// <c>common.conflict</c> if a location was deleted or changed its storage type meanwhile (nothing is
    /// stored then).
    /// </returns>
    Task<OperationResult<CheckoutCommitted>> CommitAsync(CheckoutCommit commit, CancellationToken cancellationToken);
}
