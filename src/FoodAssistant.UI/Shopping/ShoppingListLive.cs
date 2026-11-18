using FoodAssistant.Client.Sync;
using FoodAssistant.Contracts.Sync;

namespace FoodAssistant.UI.Shopping;

/// <summary>
/// Turns live messages into reload requests of the list page: item, list and store changes reload (the order only
/// after list or store changes), a resync reloads everything without announcing.
/// </summary>
internal sealed class ShoppingListLive : IDisposable
{
    private static readonly string[] Relevant = [SyncEntityTypes.ShoppingItem, SyncEntityTypes.ShoppingList, SyncEntityTypes.Store];

    private readonly ILiveUpdates _live;
    private readonly Func<Guid?> _household;
    private readonly Func<bool, bool, Task> _refresh;

    /// <param name="refresh">Called with (announce, reloadOrder).</param>
    public ShoppingListLive(ILiveUpdates live, Func<Guid?> household, Func<bool, bool, Task> refresh)
    {
        (_live, _household, _refresh) = (live, household, refresh);
        _live.Changed += OnChangedAsync;
        _live.Resynced += OnResyncedAsync;
    }

    private Task OnChangedAsync(HouseholdChangesMessage message)
    {
        if (message.HouseholdId != _household())
        {
            return Task.CompletedTask;
        }

        var types = message.Changes.Select(c => c.EntityType).ToHashSet(StringComparer.Ordinal);
        if (!types.Overlaps(Relevant))
        {
            return Task.CompletedTask;
        }

        return _refresh(true, types.Contains(SyncEntityTypes.ShoppingList) || types.Contains(SyncEntityTypes.Store));
    }

    // After a reconnect the changes in between are unknown, so everything is fetched again - without announcing.
    private Task OnResyncedAsync() => _refresh(false, true);

    public void Dispose()
    {
        _live.Changed -= OnChangedAsync;
        _live.Resynced -= OnResyncedAsync;
    }
}
