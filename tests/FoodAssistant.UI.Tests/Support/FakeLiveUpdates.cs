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

    /// <summary>When set, a start connects only once this completes.</summary>
    public TaskCompletionSource? HoldStart { get; set; }

    public async Task StartAsync(Guid householdId, CancellationToken cancellationToken)
    {
        Started.Add(householdId);
        if (HoldStart is not null)
        {
            await HoldStart.Task.ConfigureAwait(false);
        }

        State = LiveConnectionState.Connected;
        StateChanged?.Invoke();
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
