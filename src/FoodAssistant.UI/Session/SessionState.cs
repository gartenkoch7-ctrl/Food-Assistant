using FoodAssistant.Client.Accounts;
using FoodAssistant.Client.Http;
using FoodAssistant.Client.Sync;
using FoodAssistant.Contracts.Accounts;
using FoodAssistant.Contracts.Households;
using FoodAssistant.UI.Common;

namespace FoodAssistant.UI.Session;

/// <summary>
/// Who is signed in and which household is active. Scoped (one per app instance); pages read
/// <see cref="ActiveHouseholdId"/> and <see cref="CanEdit"/> and never ask the server for them again.
/// </summary>
public sealed class SessionState : IDisposable
{
    /// <summary>Local storage key of the last chosen household.</summary>
    public const string HouseholdStorageKey = "foodassistant.household";

    private readonly IAccountClient _accounts;
    private readonly IBrowserInterop _browser;
    private readonly ILiveUpdates _live;
    private readonly SessionExpiry _expiry;
    private bool _loadRequested;
    private Task? _expiryReload;

    public SessionState(IAccountClient accounts, IBrowserInterop browser, ILiveUpdates live, SessionExpiry expiry)
    {
        ArgumentNullException.ThrowIfNull(expiry);
        _accounts = accounts;
        _browser = browser;
        _live = live;
        _expiry = expiry;
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
        var result = await _accounts.GetMeAsync(cancellationToken).ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            await SetSignedOutAsync(result.Error!.Status, cancellationToken).ConfigureAwait(false);
            return;
        }

        Me = result.Value;
        if (Me!.Households.Count == 0)
        {
            ActiveHousehold = null;
            await _live.StopAsync().ConfigureAwait(false);
            SetStatus(SessionStatus.NoHousehold);
            return;
        }

        var stored = await _browser.GetItemAsync(HouseholdStorageKey, cancellationToken).ConfigureAwait(false);
        var chosen = Me.Households.FirstOrDefault(h => h.Id.ToString() == stored) ?? Me.Households[0];
        await ActivateAsync(chosen, cancellationToken).ConfigureAwait(false);
    }

    public async Task SelectHouseholdAsync(Guid householdId, CancellationToken cancellationToken = default)
    {
        var household = Me?.Households.FirstOrDefault(h => h.Id == householdId);
        if (household is null)
        {
            return;
        }

        await _browser.SetItemAsync(HouseholdStorageKey, householdId.ToString(), cancellationToken).ConfigureAwait(false);
        await ActivateAsync(household, cancellationToken).ConfigureAwait(false);
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

        await _live.StopAsync().ConfigureAwait(false);
        Me = null;
        ActiveHousehold = null;
        SetStatus(SessionStatus.SignedOut);
        return null;
    }

    public void Dispose() => _expiry.Expired -= OnSessionExpired;

    // A data call was answered with 401: ask the server again; "signed out" sends the gate to the login page.
    // Several calls failing at once trigger one reload only.
    private void OnSessionExpired()
    {
        if (Status is (SessionStatus.Ready or SessionStatus.NoHousehold) && _expiryReload is not { IsCompleted: false })
        {
            _expiryReload = LoadAsync();
        }
    }

    private async Task ActivateAsync(MeHouseholdResponse household, CancellationToken cancellationToken)
    {
        await _live.StopAsync().ConfigureAwait(false);
        ActiveHousehold = household;
        await _live.StartAsync(household.Id, cancellationToken).ConfigureAwait(false);
        SetStatus(SessionStatus.Ready);
    }

    private async Task SetSignedOutAsync(int status, CancellationToken cancellationToken)
    {
        Me = null;
        ActiveHousehold = null;
        await _live.StopAsync().ConfigureAwait(false);

        // Only an answer of "not signed in" says anything about setup; a network error keeps the login page,
        // where trying again shows the problem.
        var needsSetup = false;
        if (status == 401)
        {
            var setup = await _accounts.GetSetupStatusAsync(cancellationToken).ConfigureAwait(false);
            needsSetup = setup.IsSuccess && !setup.Value!.IsSetupComplete;
        }

        SetStatus(needsSetup ? SessionStatus.NeedsSetup : SessionStatus.SignedOut);
    }

    private void SetStatus(SessionStatus status)
    {
        Status = status;
        Changed?.Invoke();
    }
}
