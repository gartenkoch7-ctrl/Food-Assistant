using FoodAssistant.Contracts.Households;
using FoodAssistant.UI.Common;
using FoodAssistant.UI.Session;
using FoodAssistant.UI.Tests.Support;
using Microsoft.Extensions.Logging.Abstractions;

namespace FoodAssistant.UI.Tests.Session;

/// <summary>Switching the household while a reload (e.g. after a 401) is under way must not be undone by it.</summary>
public sealed class SessionStateHouseholdSwitchTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly FakeAccountClient _accounts = new() { Me = FakeAccountClient.MeWith(HouseholdRole.Member, households: 2) };
    private readonly HoldingBrowser _browser = new();
    private readonly FakeLiveUpdates _live = new();

    private SessionState Create() =>
        new(_accounts, _browser, _live, new FoodAssistant.Client.Http.SessionExpiry(), TimeProvider.System, NullLogger<SessionState>.Instance);

    [Fact]
    public async Task Switch_WhileTheReloadReadsTheStoredHousehold_StaysOnTheNewHousehold()
    {
        var session = Create();
        await session.LoadAsync(Ct);
        var second = _accounts.Me!.Households[1].Id;
        _browser.HoldGet = new TaskCompletionSource();

        var reload = session.LoadAsync(Ct);
        Assert.False(reload.IsCompleted);
        var select = session.SelectHouseholdAsync(second, Ct);
        _browser.HoldGet.SetResult();
        await Task.WhenAll(reload, select);

        Assert.Equal(second, session.ActiveHouseholdId);
        Assert.Equal(second.ToString(), _browser.Storage[SessionState.HouseholdStorageKey]);
        Assert.Equal(second, _live.Started[^1]);
    }

    [Fact]
    public async Task Switch_WhileTheReloadWaitsForTheServer_DropsTheReload()
    {
        var session = Create();
        await session.LoadAsync(Ct);
        var second = _accounts.Me!.Households[1].Id;
        _accounts.HoldMe = new TaskCompletionSource();

        var reload = session.LoadAsync(Ct);
        await session.SelectHouseholdAsync(second, Ct);
        _accounts.HoldMe.SetResult();
        await reload;

        Assert.Equal(second, session.ActiveHouseholdId);
        Assert.Equal(SessionStatus.Ready, session.Status);
    }

    [Fact]
    public async Task Switch_WhileASignOutWaitsForTheLock_DoesNotUndoTheSignOut()
    {
        var session = Create();
        await session.LoadAsync(Ct);
        var second = _accounts.Me!.Households[1].Id;
        _live.HoldStart = new TaskCompletionSource();

        var reload = session.LoadAsync(Ct);
        var signOut = session.SignOutAsync(Ct);
        await session.SelectHouseholdAsync(second, Ct);
        _live.HoldStart.SetResult();
        await Task.WhenAll(reload, signOut);

        Assert.Equal(SessionStatus.SignedOut, session.Status);
        Assert.Null(session.ActiveHouseholdId);
    }

    [Fact]
    public async Task Switch_ToAnUnknownHousehold_ChangesNothingAndKeepsTheReload()
    {
        var session = Create();
        await session.LoadAsync(Ct);
        var before = session.ActiveHouseholdId;

        await session.SelectHouseholdAsync(Guid.CreateVersion7(), Ct);

        Assert.Equal(before, session.ActiveHouseholdId);
        Assert.False(_browser.Storage.ContainsKey(SessionState.HouseholdStorageKey));
    }

    /// <summary>The in-memory browser, but a read of the stored value can be held back.</summary>
    private sealed class HoldingBrowser : IBrowserInterop
    {
        private readonly FakeBrowserInterop _inner = new();

        public TaskCompletionSource? HoldGet { get; set; }

        public Dictionary<string, string> Storage => _inner.Storage;

        public async Task<string?> GetItemAsync(string key, CancellationToken cancellationToken = default)
        {
            // The value is read before waiting: a stale read is what the old code acted on.
            var value = await _inner.GetItemAsync(key, cancellationToken);
            if (HoldGet is { } hold)
            {
                await hold.Task;
            }

            return value;
        }

        public Task SetItemAsync(string key, string value, CancellationToken cancellationToken = default) =>
            _inner.SetItemAsync(key, value, cancellationToken);

        public Task ApplyThemeAsync(string themeToken, CancellationToken cancellationToken = default) =>
            _inner.ApplyThemeAsync(themeToken, cancellationToken);

        public Task FocusElementAsync(string elementId, CancellationToken cancellationToken = default) =>
            _inner.FocusElementAsync(elementId, cancellationToken);

        public Task SetCultureCookieAsync(string culture, CancellationToken cancellationToken = default) =>
            _inner.SetCultureCookieAsync(culture, cancellationToken);
    }
}
