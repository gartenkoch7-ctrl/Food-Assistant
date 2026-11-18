using FoodAssistant.Client.Http;
using FoodAssistant.Client.Sync;
using FoodAssistant.Contracts.Households;
using FoodAssistant.UI.Session;
using FoodAssistant.UI.Tests.Support;
using Microsoft.Extensions.Logging;

namespace FoodAssistant.UI.Tests.Session;

/// <summary>The reload after a 401: it must not outlive a sign-out, loop, or fail unnoticed.</summary>
public sealed class SessionStateExpiryTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly FakeAccountClient _accounts = new() { Me = FakeAccountClient.MeWith(HouseholdRole.Member) };
    private readonly FakeLiveUpdates _live = new();
    private readonly SessionExpiry _expiry = new();
    private readonly ManualTimeProvider _time = new();
    private readonly RecordingLogger _logger = new();

    private async Task<SessionState> SignedInAsync()
    {
        var session = new SessionState(_accounts, new FakeBrowserInterop(), _live, _expiry, _time, _logger);
        await session.LoadAsync(Ct);
        return session;
    }

    [Fact]
    public async Task SignOutWhileReloadWaitsForTheServer_EndsSignedOut()
    {
        var session = await SignedInAsync();
        _accounts.HoldMe = new TaskCompletionSource();
        _expiry.Notify();

        Assert.Null(await session.SignOutAsync(Ct));
        _accounts.HoldMe.SetResult();

        Assert.Equal(SessionStatus.SignedOut, session.Status);
        Assert.Null(session.Me);
        Assert.Null(session.ActiveHouseholdId);
        Assert.Equal(LiveConnectionState.Disconnected, _live.State);
    }

    [Fact]
    public async Task SignOutWhileReloadStartsLiveUpdates_EndsSignedOut_WithLiveUpdatesStopped()
    {
        var session = await SignedInAsync();
        _live.HoldStart = new TaskCompletionSource();
        _expiry.Notify();

        var signOut = session.SignOutAsync(Ct);
        Assert.False(signOut.IsCompleted);
        _live.HoldStart.SetResult();

        Assert.Null(await signOut);
        Assert.Equal(SessionStatus.SignedOut, session.Status);
        Assert.Null(session.ActiveHouseholdId);
        Assert.Equal(LiveConnectionState.Disconnected, _live.State);
    }

    [Fact]
    public async Task Repeated401WithinTheInterval_ReloadsOnce()
    {
        var session = await SignedInAsync();

        _expiry.Notify();
        _time.Advance(SessionState.ExpiryReloadInterval - TimeSpan.FromSeconds(1));
        _expiry.Notify();

        Assert.Equal(2, _accounts.Calls.Count(c => c == "me"));
        Assert.Equal(SessionStatus.Ready, session.Status);
    }

    [Fact]
    public async Task Repeated401AfterTheInterval_ReloadsAgain()
    {
        await SignedInAsync();

        _expiry.Notify();
        _time.Advance(SessionState.ExpiryReloadInterval);
        _expiry.Notify();

        Assert.Equal(3, _accounts.Calls.Count(c => c == "me"));
    }

    [Fact]
    public async Task NewSignInAfterExpiry_ReactsToTheNext401Immediately()
    {
        var session = await SignedInAsync();
        var me = _accounts.Me;
        _accounts.Me = null;
        _expiry.Notify();
        Assert.Equal(SessionStatus.SignedOut, session.Status);

        _accounts.Me = me;
        await session.LoadAsync(Ct);
        _accounts.Me = null;
        _expiry.Notify();

        Assert.Equal(SessionStatus.SignedOut, session.Status);
    }

    [Fact]
    public async Task FailingReload_IsLogged_AndKeepsTheSession()
    {
        var session = await SignedInAsync();
        _accounts.MeException = new InvalidOperationException("test");

        _expiry.Notify();

        Assert.Equal([4200], _logger.EventIds);
        Assert.Equal(SessionStatus.Ready, session.Status);
    }

    private sealed class RecordingLogger : ILogger<SessionState>
    {
        public List<int> EventIds { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            EventIds.Add(eventId.Id);
    }
}
