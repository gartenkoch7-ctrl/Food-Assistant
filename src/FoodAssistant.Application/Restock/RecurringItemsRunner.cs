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
            try
            {
                if (await IsOpenAsync(rule.LastCreatedItemId, cancellationToken).ConfigureAwait(false))
                {
                    RestockLog.RecurringItemSkipped(logger, rule.Id);
                }
                else
                {
                    var (id, isNew) = await AddAsync(householdId, rule, cancellationToken).ConfigureAwait(false);
                    created = id;
                    added += isNew ? 1 : 0;
                }
            }
#pragma warning disable CA1031 // One broken rule must not starve the others; it moves on like any other.
            catch (Exception exception) when (exception is not OperationCanceledException)
#pragma warning restore CA1031
            {
                RestockLog.RecurringRuleFailed(logger, rule.Id, exception);
            }

            // Skipped, added or failed, the due date moves on; otherwise the rule would fire again every run.
            try
            {
                var next = RecurrenceSchedule.NextDue(rule.Frequency, rule.DayOfWeek, rule.DayOfMonth, today);
                await rules.MarkRunAsync(rule.Id, rule.Version, next, created, time.GetUtcNow(), cancellationToken)
                    .ConfigureAwait(false);
            }
#pragma warning disable CA1031 // The next run repeats it; the entry id per due date keeps that from duplicating.
            catch (Exception exception) when (exception is not OperationCanceledException)
#pragma warning restore CA1031
            {
                RestockLog.RecurringRuleFailed(logger, rule.Id, exception);
            }
        }

        activity?.SetTag("foodassistant.items", added);
        return added;
    }

    // The entry of one due date always has the same id: if the run dies after the insert but before
    // the rule is marked, the repeat finds the entry (AddInternalAsync returns a known id) instead of adding a second.
    private static Guid EntryId(DueRecurringRule rule)
    {
        Span<byte> input = stackalloc byte[20];
        rule.Id.TryWriteBytes(input);
        BitConverter.TryWriteBytes(input[16..], rule.DueOn.DayNumber);
        Span<byte> hash = stackalloc byte[32];
        System.Security.Cryptography.SHA256.HashData(input, hash);
        var bytes = hash[..16];
        bytes[6] = (byte)((bytes[6] & 0x0F) | 0x80); // version 8: custom, name-based
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);
        return new Guid(bytes, bigEndian: true);
    }

    // Open = still on a list: neither checked off nor deleted.
    private async Task<bool> IsOpenAsync(Guid? itemId, CancellationToken cancellationToken) =>
        itemId is { } id
        && await itemStore.GetItemAsync(id, includeDeleted: false, cancellationToken).ConfigureAwait(false) is { Checked: false };

    private async Task<(Guid Id, bool IsNew)> AddAsync(Guid householdId, DueRecurringRule rule, CancellationToken cancellationToken)
    {
        // The product may have been archived since: the entry then falls back to the rule's text.
        var product = rule.ProductId is { } productId
            ? await itemStore.GetProductForItemAsync(productId, cancellationToken).ConfigureAwait(false)
            : null;
        var item = new NewShoppingItem(
            EntryId(rule),
            product is null ? null : rule.ProductId,
            product?.Name ?? rule.Text,
            rule.Quantity,
            Note: null,
            product is { } p ? p.SectionKey ?? StoreSections.ForCategory(p.CategoryKey) : StoreSections.Other,
            ShoppingItemOrigin.Recurring,
            CreatedByUserId: null);
        // A repeat after a crash finds its entry; that is not a new entry for logs and metrics.
        var existed = await itemStore.GetItemAsync(item.Id, includeDeleted: true, cancellationToken).ConfigureAwait(false) is not null;
        var added = await items.AddInternalAsync(householdId, rule.ListId, item, cancellationToken).ConfigureAwait(false);
        if (!existed)
        {
            RestockLog.RecurringItemAdded(logger, rule.Id, rule.ListId);
            RestockMetrics.RecurringItemAdded();
        }

        return (added.Id, !existed);
    }
}
