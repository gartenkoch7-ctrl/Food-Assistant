namespace FoodAssistant.Application.Inventory;

/// <summary>
/// Hook after the stock of products changed (batch added, consumed, discarded, imported), e.g. for
/// the minimum-stock check. Several handlers may be registered.
/// </summary>
/// <remarks>
/// Callers (inventory, checkout, catalog on minimum-stock changes, import) resolve
/// <c>IEnumerable&lt;IStockChangedHandler&gt;</c> and call <b>all</b> handlers <b>after</b> the
/// change was saved successfully. A failing handler is logged by the caller and never fails the
/// caller's operation or keeps the other handlers from running.
/// </remarks>
public interface IStockChangedHandler
{
    /// <param name="householdId">The active household of the change.</param>
    /// <param name="productIds">Products whose stock changed (distinct).</param>
    /// <param name="cancellationToken">Cancellation of the caller.</param>
    Task OnStockChangedAsync(Guid householdId, IReadOnlyCollection<Guid> productIds, CancellationToken cancellationToken);
}
