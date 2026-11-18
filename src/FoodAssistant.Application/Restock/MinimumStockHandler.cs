using FoodAssistant.Application.Diagnostics;
using FoodAssistant.Application.Inventory;
using FoodAssistant.Application.Shopping;
using FoodAssistant.Domain.Inventory;
using FoodAssistant.Domain.Quantities;
using FoodAssistant.Domain.Shopping;
using Microsoft.Extensions.Logging;

namespace FoodAssistant.Application.Restock;

/// <summary>
/// Puts a product on the default shopping list when its stock falls below the minimum, once;
/// see <see cref="RestockDecision"/> for the rule.
/// </summary>
public sealed class MinimumStockHandler(
    IRestockStore store,
    ShoppingListService lists,
    ShoppingItemService items,
    ILogger<MinimumStockHandler> logger) : IStockChangedHandler
{
    public async Task OnStockChangedAsync(
        Guid householdId,
        IReadOnlyCollection<Guid> productIds,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(productIds);
        foreach (var productId in productIds.Distinct())
        {
            await CheckAsync(householdId, productId, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task CheckAsync(Guid householdId, Guid productId, CancellationToken cancellationToken)
    {
        using var activity = Telemetry.Source.StartActivity("foodassistant.restock.minimum_stock");
        var state = await store.GetProductStockAsync(productId, cancellationToken).ConfigureAwait(false);
        if (state is null)
        {
            activity?.SetTag("foodassistant.outcome", "product_gone");
            return;
        }

        var total = state.Minimum is { } minimum
            ? MinimumStockPolicy.TotalStock(state.Batches, minimum.Unit)
            : Quantity.Zero(Unit.Piece);
        var action = RestockDecision.Decide(state.Minimum, total, state.Triggered, state.OnOpenList);
        activity?.SetTag("foodassistant.outcome", action.Kind.ToString());

        switch (action.Kind)
        {
            case RestockKind.AddToList:
                await AddAsync(householdId, productId, state, action.Amount, cancellationToken).ConfigureAwait(false);
                break;
            case RestockKind.Arm:
                await store.SetTriggeredAsync(productId, true, cancellationToken).ConfigureAwait(false);
                break;
            case RestockKind.Disarm:
                await store.SetTriggeredAsync(productId, false, cancellationToken).ConfigureAwait(false);
                break;
        }
    }

    private async Task AddAsync(
        Guid householdId,
        Guid productId,
        ProductStockState state,
        Quantity? amount,
        CancellationToken cancellationToken)
    {
        // Claim first: of two parallel checks only the one that flips the flag adds the entry.
        if (!await store.SetTriggeredAsync(productId, true, cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        try
        {
            var list = await lists.EnsureDefaultListAsync(householdId, cancellationToken).ConfigureAwait(false);
            var item = new NewShoppingItem(
                Guid.CreateVersion7(),
                productId,
                state.Name,
                amount,
                Note: null,
                state.SectionKey ?? StoreSections.ForCategory(state.CategoryKey),
                ShoppingItemOrigin.MinimumStock,
                CreatedByUserId: null);
            await items.AddInternalAsync(householdId, list.Id, item, cancellationToken).ConfigureAwait(false);
            RestockLog.RestockItemAdded(logger, productId, list.Id);
            RestockMetrics.MinimumStockItemAdded();
        }
        catch
        {
            // No entry exists: take the claim back so the next stock change tries again.
            await store.SetTriggeredAsync(productId, false, CancellationToken.None).ConfigureAwait(false);
            throw;
        }
    }
}
