using FoodAssistant.Client.Sync;
using FoodAssistant.Contracts.Households;
using FoodAssistant.UI.Session;
using FoodAssistant.UI.Tests.Support;
using Microsoft.Extensions.Logging.Abstractions;

namespace FoodAssistant.UI.Tests.Session;

/// <summary>The real SignalR client against a hub that cannot be reached must not hold up the session's transitions.</summary>
public sealed class SessionStateLiveStartTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // A closed local port refuses at once; the long retry pause keeps the background loop quiet during the test.
    private static SignalRLiveUpdates UnreachableLive() =>
        new(new Uri("http://127.0.0.1:1"), NullLogger<SignalRLiveUpdates>.Instance, retryInterval: TimeSpan.FromMinutes(5));

    [Fact]
    public async Task LoadAndSignOut_DoNotWaitForAnUnreachableHub()
    {
        await using var live = UnreachableLive();
        var accounts = new FakeAccountClient { Me = FakeAccountClient.MeWith(HouseholdRole.Member) };
        var session = new SessionState(
            accounts, new FakeBrowserInterop(), live, new Client.Http.SessionExpiry(), TimeProvider.System, NullLogger<SessionState>.Instance);

        await session.LoadAsync(Ct).WaitAsync(TimeSpan.FromSeconds(5), Ct);
        Assert.Equal(SessionStatus.Ready, session.Status);
        var error = await session.SignOutAsync(Ct).WaitAsync(TimeSpan.FromSeconds(5), Ct);

        Assert.Null(error);
        Assert.Equal(SessionStatus.SignedOut, session.Status);
        Assert.Equal(LiveConnectionState.Disconnected, live.State);
    }

    [Fact]
    public async Task StartAgainstAnUnreachableHub_ReturnsAtOnce_AndEndsDisconnected()
    {
        await using var live = UnreachableLive();
        var changed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        live.StateChanged += () =>
        {
            if (live.State == LiveConnectionState.Disconnected)
            {
                changed.TrySetResult();
            }
        };

        await live.StartAsync(Guid.NewGuid(), Ct).WaitAsync(TimeSpan.FromSeconds(5), Ct);

        await changed.Task.WaitAsync(TimeSpan.FromSeconds(30), Ct);
        Assert.Equal(LiveConnectionState.Disconnected, live.State);
    }
}
