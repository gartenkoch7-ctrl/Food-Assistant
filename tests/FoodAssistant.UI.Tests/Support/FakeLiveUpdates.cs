using FoodAssistant.Client.Sync;
using FoodAssistant.Contracts.Sync;

namespace FoodAssistant.UI.Tests.Support;

/// <summary>Live updates without a connection; tests raise changes by hand and read which households were joined.</summary>
public sealed class FakeLiveUpdates : ILiveUpdates
{
    public event Func<HouseholdChangesMessage, Task>? Changed;

    public event Func<Task>? Resynced;

    public event Action? StateChanged;

    public LiveConnectionState State { get; private set; } = LiveConnectionState.Disconnected;

    public List<Guid> Started { get; } = [];

    public int Stopped { get; private set; }

    public Task StartAsync(Guid householdId, CancellationToken cancellationToken)
    {
        Started.Add(householdId);
        State = LiveConnectionState.Connected;
        StateChanged?.Invoke();
        return Task.CompletedTask;
    }

    public Task StopAsync()
    {
        Stopped++;
        State = LiveConnectionState.Disconnected;
        StateChanged?.Invoke();
        return Task.CompletedTask;
    }

    public Task RaiseChangedAsync(HouseholdChangesMessage message) => Changed?.Invoke(message) ?? Task.CompletedTask;

    public Task RaiseResyncedAsync() => Resynced?.Invoke() ?? Task.CompletedTask;
}
