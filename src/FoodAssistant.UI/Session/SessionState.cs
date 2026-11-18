using FoodAssistant.Client.Accounts;
using FoodAssistant.Client.Sync;
using FoodAssistant.Contracts.Accounts;
using FoodAssistant.Contracts.Households;
using FoodAssistant.UI.Common;

namespace FoodAssistant.UI.Session;

/// <summary>
/// Who is signed in and which household is active. Scoped (one per app instance); pages read
/// <see cref="ActiveHouseholdId"/> and <see cref="CanEdit"/> and never ask the server for them again.
/// </summary>
public sealed class SessionState(IAccountClient accounts, IBrowserInterop browser, ILiveUpdates live)
{
    /// <summary>Local storage key of the last chosen household.</summary>
    public const string HouseholdStorageKey = "foodassistant.household";

    private bool _loadRequested;

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
        var result = await accounts.GetMeAsync(cancellationToken).ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            await SetSignedOutAsync(result.Error!.Status, cancellationToken).ConfigureAwait(false);
            return;
        }

        Me = result.Value;
        if (Me!.Households.Count == 0)
        {
            ActiveHousehold = null;
            await live.StopAsync().ConfigureAwait(false);
            SetStatus(SessionStatus.NoHousehold);
            return;
        }

        var stored = await browser.GetItemAsync(HouseholdStorageKey, cancellationToken).ConfigureAwait(false);
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

        await browser.SetItemAsync(HouseholdStorageKey, householdId.ToString(), cancellationToken).ConfigureAwait(false);
        await ActivateAsync(household, cancellationToken).ConfigureAwait(false);
    }

    public async Task SignOutAsync(CancellationToken cancellationToken = default)
    {
        await live.StopAsync().ConfigureAwait(false);
        await accounts.LogoutAsync(cancellationToken).ConfigureAwait(false);
        Me = null;
        ActiveHousehold = null;
        SetStatus(SessionStatus.SignedOut);
    }

    private async Task ActivateAsync(MeHouseholdResponse household, CancellationToken cancellationToken)
    {
        await live.StopAsync().ConfigureAwait(false);
        ActiveHousehold = household;
        await live.StartAsync(household.Id, cancellationToken).ConfigureAwait(false);
        SetStatus(SessionStatus.Ready);
    }

    private async Task SetSignedOutAsync(int status, CancellationToken cancellationToken)
    {
        Me = null;
        ActiveHousehold = null;
        await live.StopAsync().ConfigureAwait(false);

        // Only an answer of "not signed in" says anything about setup; a network error keeps the login page,
        // where trying again shows the problem.
        var needsSetup = false;
        if (status == 401)
        {
            var setup = await accounts.GetSetupStatusAsync(cancellationToken).ConfigureAwait(false);
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
