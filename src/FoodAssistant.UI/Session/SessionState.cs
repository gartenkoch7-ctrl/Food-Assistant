using FoodAssistant.Client.Accounts;
using FoodAssistant.Client.Http;
using FoodAssistant.Client.Sync;
using FoodAssistant.Contracts.Accounts;
using FoodAssistant.Contracts.Households;
using FoodAssistant.UI.Common;
using Microsoft.Extensions.Logging;

namespace FoodAssistant.UI.Session;

/// <summary>
/// Who is signed in and which household is active. Scoped (one per app instance); pages read
/// <see cref="ActiveHouseholdId"/> and <see cref="CanEdit"/> and never ask the server for them again.
/// </summary>
public sealed class SessionState : IDisposable
{
    /// <summary>Local storage key of the last chosen household.</summary>
    public const string HouseholdStorageKey = "foodassistant.household";

    /// <summary>A 401 from a data call reloads the session at most once in this window.</summary>
    public static readonly TimeSpan ExpiryReloadInterval = TimeSpan.FromSeconds(30);

    private readonly IAccountClient _accounts;
    private readonly IBrowserInterop _browser;
    private readonly ILiveUpdates _live;
    private readonly SessionExpiry _expiry;
    private readonly TimeProvider _time;
    private readonly ILogger<SessionState> _logger;

    // State changes (live connection + status) run one at a time; each load or sign-out gets a new
    // generation, and a change of an older generation is dropped (e.g. a reload that ends after sign-out).
    private readonly SemaphoreSlim _transition = new(1, 1);
    private int _generation;
    private int _signOutsPending;
    private bool _loadRequested;
    private Task? _expiryReload;
    private DateTimeOffset? _lastExpiryReload;

    public SessionState(
        IAccountClient accounts,
        IBrowserInterop browser,
        ILiveUpdates live,
        SessionExpiry expiry,
        TimeProvider time,
        ILogger<SessionState> logger)
    {
        ArgumentNullException.ThrowIfNull(expiry);
        _accounts = accounts;
        _browser = browser;
        _live = live;
        _expiry = expiry;
        _time = time;
        _logger = logger;
        expiry.Expired += OnSessionExpired;
    }

    public MeResponse? Me { get; private set; }

    public MeHouseholdResponse? ActiveHousehold { get; private set; }

    public Guid? ActiveHouseholdId => ActiveHousehold?.Id;

    public SessionStatus Status { get; private set; } = SessionStatus.Loading;

    /// <summary>Member or owner: may change data. Restricted people only add and check off shopping items (ADR-0002).</summary>
    public bool CanEdit => ActiveHousehold is { Role: >= HouseholdRole.Member };

    public bool IsOwner => ActiveHousehold is { Role: HouseholdRole.Owner };

    public event Action? Changed;

    /// <summary>Loads the session once; later calls do nothing. Use <see cref="LoadAsync"/> to force a refresh.</summary>
    public Task EnsureLoadedAsync(CancellationToken cancellationToken = default) =>
        _loadRequested ? Task.CompletedTask : LoadAsync(cancellationToken);

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        _loadRequested = true;
        var generation = Interlocked.Increment(ref _generation);
        var result = await _accounts.GetMeAsync(cancellationToken).ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            var needsSetup = await NeedsSetupAsync(result.Error!.Status, cancellationToken).ConfigureAwait(false);
            await TransitionAsync(generation, () => SignedOutAsync(needsSetup ? SessionStatus.NeedsSetup : SessionStatus.SignedOut))
                .ConfigureAwait(false);
            return;
        }

        var me = result.Value!;
        if (me.Households.Count == 0)
        {
            await TransitionAsync(generation, async () =>
            {
                await _live.StopAsync().ConfigureAwait(false);
                Me = me;
                ActiveHousehold = null;
                return SessionStatus.NoHousehold;
            }).ConfigureAwait(false);
            return;
        }

        // The stored household is read inside the transition: a switch that finished while /me was in
        // flight has written its choice by then, so the reload cannot re-activate the old household.
        await TransitionAsync(generation, async () =>
        {
            var stored = await _browser.GetItemAsync(HouseholdStorageKey, cancellationToken).ConfigureAwait(false);
            var chosen = me.Households.FirstOrDefault(h => h.Id.ToString() == stored) ?? me.Households[0];
            return await ActivateAsync(me, chosen, cancellationToken).ConfigureAwait(false);
        }).ConfigureAwait(false);
    }

    public async Task SelectHouseholdAsync(Guid householdId, CancellationToken cancellationToken = default)
    {
        // A sign-out in progress wins over a switch (shared devices must end up signed out).
        if (Volatile.Read(ref _signOutsPending) > 0 || (Me?.Households.All(h => h.Id != householdId) ?? true))
        {
            return;
        }

        // A new generation drops a reload that is still waiting for /me; it started before this choice.
        var generation = Interlocked.Increment(ref _generation);
        await TransitionAsync(generation, async () =>
        {
            // Looked up again: a sign-out or reload may have changed the households since the check above.
            var household = Me?.Households.FirstOrDefault(h => h.Id == householdId);
            if (household is null)
            {
                return Status;
            }

            await _browser.SetItemAsync(HouseholdStorageKey, householdId.ToString(), cancellationToken).ConfigureAwait(false);
            return await ActivateAsync(Me!, household, cancellationToken).ConfigureAwait(false);
        }).ConfigureAwait(false);
    }

    /// <summary>Ends the session on the server, then here.</summary>
    /// <returns>
    /// <c>null</c> when signed out. Otherwise the error: the HttpOnly cookie is still valid, so the
    /// session stays as it is and the caller shows the error (a shared device must not look signed out).
    /// </returns>
    public async Task<ApiError?> SignOutAsync(CancellationToken cancellationToken = default)
    {
        var result = await _accounts.LogoutAsync(cancellationToken).ConfigureAwait(false);

        // 401: the server no longer knows the session, which is the goal of signing out.
        if (!result.IsSuccess && result.Error!.Status != 401)
        {
            return result.Error;
        }

        // A reload still in flight belongs to the old session and must not sign anyone back in.
        // A household switch must not take over this generation while the sign-out waits for the lock.
        Interlocked.Increment(ref _signOutsPending);
        try
        {
            var generation = Interlocked.Increment(ref _generation);
            await TransitionAsync(generation, () => SignedOutAsync(SessionStatus.SignedOut)).ConfigureAwait(false);
        }
        finally
        {
            Interlocked.Decrement(ref _signOutsPending);
        }

        return null;
    }

    // _transition is not disposed: a load still in flight when the scope ends must be able to release it,
    // and a SemaphoreSlim without AvailableWaitHandle holds no native resource.
    public void Dispose() => _expiry.Expired -= OnSessionExpired;

    // A data call was answered with 401: ask the server again; "signed out" sends the gate to the login page.
    // Several calls failing at once trigger one reload only, and a data endpoint that keeps answering 401
    // while /me answers 200 must not cause a reload loop, hence at most one reload per interval.
    private void OnSessionExpired()
    {
        if (Status is not (SessionStatus.Ready or SessionStatus.NoHousehold) || _expiryReload is { IsCompleted: false })
        {
            return;
        }

        var now = _time.GetUtcNow();
        if (_lastExpiryReload is { } last && now - last < ExpiryReloadInterval)
        {
            return;
        }

        _lastExpiryReload = now;
        _expiryReload = ReloadAfterExpiryAsync();
    }

    // Nobody awaits this reload, so its failure is logged here instead of getting lost.
    private async Task ReloadAfterExpiryAsync()
    {
        try
        {
            await LoadAsync().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            SessionLog.ExpiryReloadFailed(_logger, exception);
        }
    }

    private async Task<SessionStatus> ActivateAsync(MeResponse me, MeHouseholdResponse household, CancellationToken cancellationToken)
    {
        await _live.StopAsync().ConfigureAwait(false);
        Me = me;
        ActiveHousehold = household;
        await _live.StartAsync(household.Id, cancellationToken).ConfigureAwait(false);
        return SessionStatus.Ready;
    }

    private async Task<SessionStatus> SignedOutAsync(SessionStatus status)
    {
        Me = null;
        ActiveHousehold = null;

        // The throttle only breaks a "/me 200, data 401" loop; a new sign-in starts fresh.
        _lastExpiryReload = null;
        await _live.StopAsync().ConfigureAwait(false);
        return status;
    }

    // Only an answer of "not signed in" says anything about setup; a network error keeps the login page,
    // where trying again shows the problem.
    private async Task<bool> NeedsSetupAsync(int status, CancellationToken cancellationToken)
    {
        if (status != 401)
        {
            return false;
        }

        var setup = await _accounts.GetSetupStatusAsync(cancellationToken).ConfigureAwait(false);
        return setup.IsSuccess && !setup.Value!.IsSetupComplete;
    }

    // Changed is raised outside the lock, so a handler may start the next load or sign-out.
    private async Task TransitionAsync(int generation, Func<Task<SessionStatus>> change)
    {
        await _transition.WaitAsync().ConfigureAwait(false);
        try
        {
            if (generation != Volatile.Read(ref _generation))
            {
                return;
            }

            Status = await change().ConfigureAwait(false);
        }
        finally
        {
            _transition.Release();
        }

        Changed?.Invoke();
    }
}
