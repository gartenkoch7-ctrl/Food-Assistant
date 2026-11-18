using FoodAssistant.Client.Http;
using FoodAssistant.Client.Sync;
using FoodAssistant.Contracts.Households;
using FoodAssistant.UI.Session;
using FoodAssistant.UI.Tests.Support;

namespace FoodAssistant.UI.Tests.Session;

public sealed class SessionStateTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly FakeAccountClient _accounts = new();
    private readonly FakeBrowserInterop _browser = new();
    private readonly FakeLiveUpdates _live = new();

    private SessionState Create() => new(_accounts, _browser, _live, new SessionExpiry());

    [Fact]
    public async Task SignedIn_ActivatesTheFirstHousehold_AndStartsLiveUpdates()
    {
        _accounts.Me = FakeAccountClient.MeWith(HouseholdRole.Member, households: 2);
        var session = Create();

        await session.LoadAsync(Ct);

        Assert.Equal(SessionStatus.Ready, session.Status);
        Assert.Equal(FakeAccountClient.TestHouseholdId, session.ActiveHouseholdId);
        Assert.Equal([FakeAccountClient.TestHouseholdId], _live.Started);
        Assert.True(session.CanEdit);
        Assert.False(session.IsOwner);
    }

    [Fact]
    public async Task StoredHousehold_IsPreferred()
    {
        _accounts.Me = FakeAccountClient.MeWith(HouseholdRole.Owner, households: 2);
        var second = _accounts.Me.Households[1];
        _browser.Storage[SessionState.HouseholdStorageKey] = second.Id.ToString();
        var session = Create();

        await session.LoadAsync(Ct);

        Assert.Equal(second.Id, session.ActiveHouseholdId);
        Assert.True(session.IsOwner);
    }

    [Fact]
    public async Task RestrictedRole_CannotEdit()
    {
        _accounts.Me = FakeAccountClient.MeWith(HouseholdRole.Restricted);
        var session = Create();

        await session.LoadAsync(Ct);

        Assert.False(session.CanEdit);
    }

    [Fact]
    public async Task Unauthorized_WithoutAccounts_NeedsSetup()
    {
        _accounts.IsSetupComplete = false;
        var session = Create();

        await session.LoadAsync(Ct);

        Assert.Equal(SessionStatus.NeedsSetup, session.Status);
    }

    [Fact]
    public async Task Unauthorized_WithAccounts_IsSignedOut()
    {
        var session = Create();

        await session.LoadAsync(Ct);

        Assert.Equal(SessionStatus.SignedOut, session.Status);
        Assert.Null(session.ActiveHouseholdId);
    }

    [Fact]
    public async Task NoHouseholds_ShowsTheHint()
    {
        _accounts.Me = FakeAccountClient.MeWith(HouseholdRole.Member, households: 0);
        var session = Create();

        await session.LoadAsync(Ct);

        Assert.Equal(SessionStatus.NoHousehold, session.Status);
    }

    [Fact]
    public async Task SelectingAHousehold_StoresItAndRestartsLiveUpdates()
    {
        _accounts.Me = FakeAccountClient.MeWith(HouseholdRole.Member, households: 2);
        var session = Create();
        await session.LoadAsync(Ct);
        var second = _accounts.Me.Households[1];

        await session.SelectHouseholdAsync(second.Id, Ct);

        Assert.Equal(second.Id, session.ActiveHouseholdId);
        Assert.Equal(second.Id.ToString(), _browser.Storage[SessionState.HouseholdStorageKey]);
        Assert.Equal(second.Id, _live.Started[^1]);
    }

    [Fact]
    public async Task SelectingAForeignHousehold_IsIgnored()
    {
        _accounts.Me = FakeAccountClient.MeWith(HouseholdRole.Member);
        var session = Create();
        await session.LoadAsync(Ct);

        await session.SelectHouseholdAsync(Guid.NewGuid(), Ct);

        Assert.Equal(FakeAccountClient.TestHouseholdId, session.ActiveHouseholdId);
        Assert.Empty(_browser.Storage);
    }

    [Fact]
    public async Task SignOut_LogsOutAndStopsLiveUpdates()
    {
        _accounts.Me = FakeAccountClient.MeWith(HouseholdRole.Member);
        var session = Create();
        await session.LoadAsync(Ct);
        var changes = 0;
        session.Changed += () => changes++;

        await session.SignOutAsync(Ct);

        Assert.Equal(SessionStatus.SignedOut, session.Status);
        Assert.Null(session.Me);
        Assert.Contains("logout", _accounts.Calls);
        Assert.Equal(LiveConnectionState.Disconnected, _live.State);
        Assert.True(changes > 0);
    }

    [Fact]
    public async Task EnsureLoaded_AsksTheServerOnlyOnce()
    {
        _accounts.Me = FakeAccountClient.MeWith(HouseholdRole.Member);
        var session = Create();

        await session.EnsureLoadedAsync(Ct);
        await session.EnsureLoadedAsync(Ct);

        Assert.Single(_accounts.Calls, "me");
    }
}
