using FoodAssistant.Application.Diagnostics;
using FoodAssistant.Application.Households;
using FoodAssistant.Application.Shopping;
using FoodAssistant.Domain.Shopping;
using Microsoft.Extensions.Logging;

namespace FoodAssistant.Application.Restock;

/// <summary>
/// Adds the entries of recurring rules that are due (docs/kb/einkauf.md): once per due date, and not
/// at all while the entry created last is still open, so nothing piles up.
/// </summary>
public sealed class RecurringItemsRunner(
    IRecurringRuleStore rules,
    IShoppingItemStore itemStore,
    ShoppingItemService items,
    IHouseholdCalendar calendar,
    IHouseholdContext householdContext,
    TimeProvider time,
    ILogger<RecurringItemsRunner> logger) : IRecurringItemsRunner
{
    public async Task<int> RunAsync(CancellationToken cancellationToken)
    {
        var householdId = householdContext.HouseholdId
            ?? throw new InvalidOperationException("The recurring run needs an active household.");
        using var activity = Telemetry.Source.StartActivity("foodassistant.restock.recurring_run");

        var today = (await calendar.GetTodayAsync(cancellationToken).ConfigureAwait(false)).Today;
        var added = 0;
        foreach (var rule in await rules.ListDueAsync(today, cancellationToken).ConfigureAwait(false))
        {
            Guid? created = null;
            if (await IsOpenAsync(rule.LastCreatedItemId, cancellationToken).ConfigureAwait(false))
            {
                RestockLog.RecurringItemSkipped(logger, rule.Id);
            }
            else
            {
                created = await AddAsync(householdId, rule, cancellationToken).ConfigureAwait(false);
                added++;
            }

            // Skipped or added, the due date moves on; otherwise the rule would fire again every run.
            var next = RecurrenceSchedule.NextDue(rule.Frequency, rule.DayOfWeek, rule.DayOfMonth, today);
            await rules.MarkRunAsync(rule.Id, next, created, time.GetUtcNow(), cancellationToken).ConfigureAwait(false);
        }

        activity?.SetTag("foodassistant.items", added);
        return added;
    }

    // Open = still on a list: neither checked off nor deleted.
    private async Task<bool> IsOpenAsync(Guid? itemId, CancellationToken cancellationToken) =>
        itemId is { } id
        && await itemStore.GetItemAsync(id, includeDeleted: false, cancellationToken).ConfigureAwait(false) is { Checked: false };

    private async Task<Guid> AddAsync(Guid householdId, DueRecurringRule rule, CancellationToken cancellationToken)
    {
        // The product may have been archived since: the entry then falls back to the rule's text.
        var product = rule.ProductId is { } productId
            ? await itemStore.GetProductForItemAsync(productId, cancellationToken).ConfigureAwait(false)
            : null;
        var item = new NewShoppingItem(
            Guid.CreateVersion7(),
            product is null ? null : rule.ProductId,
            product?.Name ?? rule.Text,
            rule.Quantity,
            Note: null,
            product is { } p ? p.SectionKey ?? StoreSections.ForCategory(p.CategoryKey) : StoreSections.Other,
            ShoppingItemOrigin.Recurring,
            CreatedByUserId: null);
        var added = await items.AddInternalAsync(householdId, rule.ListId, item, cancellationToken).ConfigureAwait(false);
        RestockLog.RecurringItemAdded(logger, rule.Id, rule.ListId);
        RestockMetrics.RecurringItemAdded();
        return added.Id;
    }
}
