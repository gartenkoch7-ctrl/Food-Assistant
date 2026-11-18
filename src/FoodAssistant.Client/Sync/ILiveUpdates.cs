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

    /// <summary>
    /// Begins connecting and returns quickly. Contract (SessionState calls it while holding its
    /// transition lock): never throws, never waits for the connection handshake to finish and never
    /// waits for <see cref="Changed"/>/<see cref="Resynced"/> handlers; connection problems are
    /// reported through <see cref="State"/> and <see cref="StateChanged"/>.
    /// </summary>
    Task StartAsync(Guid householdId, CancellationToken cancellationToken);

    Task StopAsync();
}
