using FoodAssistant.Client.Http;
using FoodAssistant.Client.Sync;
using FoodAssistant.Contracts.Households;
using FoodAssistant.UI.Session;
using FoodAssistant.UI.Tests.Support;
using Microsoft.Extensions.Logging.Abstractions;

namespace FoodAssistant.UI.Tests.Session;

/// <summary>Sign-out failures and an expired session: the UI must never look signed out while the cookie is valid.</summary>
public sealed class SessionStateSignOutTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly FakeAccountClient _accounts = new() { Me = FakeAccountClient.MeWith(HouseholdRole.Member) };
    private readonly FakeBrowserInterop _browser = new();
    private readonly FakeLiveUpdates _live = new();
    private readonly SessionExpiry _expiry = new();

    private async Task<SessionState> SignedInAsync()
    {
        var session = new SessionState(_accounts, _browser, _live, _expiry, TimeProvider.System, NullLogger<SessionState>.Instance);
        await session.LoadAsync(Ct);
        return session;
    }

    [Theory]
    [InlineData(0, ClientErrorCodes.Network)]
    [InlineData(429, "common.rate_limited")]
    [InlineData(500, "common.internal_error")]
    public async Task FailedSignOut_KeepsTheSession_AndReturnsTheError(int status, string code)
    {
        var session = await SignedInAsync();
        _accounts.LogoutError = FakeAccountClient.Error(status, code);

        var error = await session.SignOutAsync(Ct);

        Assert.Equal(code, error!.ErrorCode);
        Assert.Equal(SessionStatus.Ready, session.Status);
        Assert.NotNull(session.Me);
        Assert.Equal(LiveConnectionState.Connected, _live.State);
    }

    [Fact]
    public async Task SignOut_AnsweredWith401_CountsAsSignedOut()
    {
        var session = await SignedInAsync();
        _accounts.LogoutError = FakeAccountClient.Error(401, "common.unauthorized");

        var error = await session.SignOutAsync(Ct);

        Assert.Null(error);
        Assert.Equal(SessionStatus.SignedOut, session.Status);
        Assert.Null(session.Me);
    }

    [Fact]
    public async Task SuccessfulSignOut_ReturnsNoError()
    {
        var session = await SignedInAsync();

        Assert.Null(await session.SignOutAsync(Ct));
        Assert.Equal(SessionStatus.SignedOut, session.Status);
    }

    [Fact]
    public async Task ExpiredSession_ReloadsAndEndsSignedOut()
    {
        var session = await SignedInAsync();
        _accounts.Me = null;

        _expiry.Notify();

        Assert.Equal(SessionStatus.SignedOut, session.Status);
        Assert.Null(session.ActiveHouseholdId);
        Assert.Equal(2, _accounts.Calls.Count(c => c == "me"));
    }

    [Fact]
    public async Task ExpiryWhileSignedOut_DoesNotAskAgain()
    {
        _accounts.Me = null;
        var session = await SignedInAsync();

        _expiry.Notify();

        Assert.Single(_accounts.Calls, "me");
    }

    [Fact]
    public async Task Disposed_StopsListening()
    {
        var session = await SignedInAsync();
        session.Dispose();

        _expiry.Notify();

        Assert.Single(_accounts.Calls, "me");
    }
}
