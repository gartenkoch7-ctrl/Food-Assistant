using FoodAssistant.Contracts.Sync;

namespace FoodAssistant.Client.Sync;

/// <summary>
/// Live changes of the active household. Pages subscribe to <see cref="Changed"/> and reload what
/// the message names; after a reconnect <see cref="Resynced"/> tells them to reload everything.
/// </summary>
public interface ILiveUpdates
{
    event Func<HouseholdChangesMessage, Task>? Changed;

    /// <summary>Raised after a reconnect, because changes may have been missed.</summary>
    event Func<Task>? Resynced;

    event Action? StateChanged;

    LiveConnectionState State { get; }

    Task StartAsync(Guid householdId, CancellationToken cancellationToken);

    Task StopAsync();
}
