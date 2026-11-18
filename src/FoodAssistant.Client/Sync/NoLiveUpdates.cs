using FoodAssistant.Contracts.Sync;

namespace FoodAssistant.Client.Sync;

/// <summary>Placeholder until the SignalR client (track E1) replaces it: never connects, never raises events.</summary>
internal sealed class NoLiveUpdates : ILiveUpdates
{
    // Declared so the interface is satisfied; nothing ever raises them.
#pragma warning disable CS0067
    public event Func<HouseholdChangesMessage, Task>? Changed;

    public event Func<Task>? Resynced;

    public event Action? StateChanged;
#pragma warning restore CS0067

    public LiveConnectionState State => LiveConnectionState.Disconnected;

    public Task StartAsync(Guid householdId, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StopAsync() => Task.CompletedTask;
}
