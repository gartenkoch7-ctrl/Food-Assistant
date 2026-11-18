using FoodAssistant.Application.Sync;
using Microsoft.Extensions.Logging;

namespace FoodAssistant.Infrastructure.Sync;

/// <summary>Announces the pending change-log entries after a commit; shared by both sync interceptors.</summary>
internal sealed class SyncChangePublisher(
    PendingChangeNotifications pending,
    IHouseholdChangeNotifier notifier,
    ILogger<SyncChangePublisher> logger)
{
    /// <summary>Publishes and forgets all pending entries. Never throws: the data is already committed.</summary>
    public async Task PublishPendingAsync(CancellationToken cancellationToken)
    {
        var entries = pending.TakeAll();
        foreach (var household in entries.GroupBy(e => e.HouseholdId))
        {
            // An entity changed by several saves of one transaction is announced once, with its latest sequence.
            var changes = household
                .GroupBy(e => (e.EntityType, e.EntityId))
                .Select(g => g.MaxBy(e => e.Sequence)!)
                .OrderBy(e => e.Sequence)
                .Select(e => new HouseholdChange(e.EntityType, e.EntityId, e.Sequence, e.Deleted))
                .ToList();
            try
            {
                await notifier.PublishAsync(household.Key, changes, cancellationToken).ConfigureAwait(false);
            }
#pragma warning disable CA1031 // Best effort after the commit: any failure (also cancellation) must not fail the save.
            catch (Exception exception)
#pragma warning restore CA1031
            {
                SyncLog.ChangeNotificationFailed(logger, household.Key, exception);
            }
        }
    }
}
