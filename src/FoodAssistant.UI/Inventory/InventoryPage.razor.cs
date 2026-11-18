using System.Globalization;
using FoodAssistant.Client.Http;
using FoodAssistant.Contracts.Catalog;
using FoodAssistant.Contracts.Common;
using FoodAssistant.Contracts.Inventory;
using FoodAssistant.Contracts.Sync;
using MudBlazor;

namespace FoodAssistant.UI.Inventory;

/// <summary>
/// The stock overview: filters, products with their earliest expiry, adding batches and the product
/// sheet. Reloads on live changes; changes of other members are announced to screen readers.
/// </summary>
public sealed partial class InventoryPage
{
    private static readonly string[] RelevantEntities = [SyncEntityTypes.Batch, SyncEntityTypes.Product, SyncEntityTypes.StorageLocation];

    private readonly List<InventoryProductDto> _items = [];
    private IReadOnlyList<StorageLocationDto> _locations = [];
    private string? _status;
    private Guid? _locationId;
    private string _query = string.Empty;
    private string? _next;
    private bool _loading = true;
    private int _generation;
    private ApiError? _error;

    // Own actions come back as live changes too; only changes of others are announced. While a
    // dialog of this page is open, changes are almost always the person's own actions there.
    private DateTimeOffset _ownChangeUntil;
    private bool _dialogOpen;

    private IEnumerable<(string? Value, string Label)> Filters =>
    [
        (null, T["FilterAll"]),
        ("soon", T["FilterSoon"]),
        ("expired", T["FilterExpired"]),
    ];

    protected override async Task OnInitializedAsync()
    {
        Live.Changed += OnLiveChangedAsync;
        Live.Resynced += OnResyncedAsync;
        await LoadLocationsAsync();
        await ReloadAsync();
    }

    private string SecondaryOf(InventoryProductDto item)
    {
        var count = item.Batches.Count == 1
            ? T["BatchCountOne"].Value
            : string.Format(CultureInfo.CurrentCulture, T["BatchCountMany"].Value, item.Batches.Count);
        var totals = item.Totals.Select(t => InventoryFields.Quantity(t, Common, CultureInfo.CurrentCulture));
        var places = item.Batches.Select(b => InventoryFields.LocationName(b.StorageLocationId, _locations))
            .Where(name => name.Length > 0).Distinct();
        return string.Join(" · ", new[] { count }.Concat(totals).Append(string.Join(", ", places)).Where(p => p.Length > 0));
    }

    private Task FilterAsync(string? status)
    {
        _status = status;
        return ReloadAsync();
    }

    private Task LocationAsync(Guid? locationId)
    {
        _locationId = locationId;
        return ReloadAsync();
    }

    private Task SearchAsync(string query)
    {
        _query = query ?? string.Empty;
        return ReloadAsync();
    }

    private async Task LoadLocationsAsync()
    {
        if (Session.ActiveHouseholdId is { } household)
        {
            _locations = (await Catalog.ListStorageLocationsAsync(household, CancellationToken.None)).Value ?? _locations;
        }
    }

    private async Task ReloadAsync()
    {
        _loading = true;
        var generation = ++_generation;
        var page = await LoadAsync(cursor: null);
        if (generation != _generation)
        {
            return; // A newer filter or reload started meanwhile; its answer wins.
        }

        _items.Clear();
        _items.AddRange(page?.Items ?? []);
        _next = page?.NextCursor;
        _loading = false;
    }

    private async Task LoadMoreAsync()
    {
        _loading = true;
        var generation = _generation;
        var page = await LoadAsync(_next);
        if (generation != _generation)
        {
            return;
        }

        _items.AddRange(page?.Items ?? []);
        _next = page?.NextCursor;
        _loading = false;
    }

    private async Task<CursorPage<InventoryProductDto>?> LoadAsync(string? cursor)
    {
        if (Session.ActiveHouseholdId is not { } household)
        {
            return null;
        }

        var result = await Inventory.ListAsync(household, _status, _locationId, _query, cursor, null, CancellationToken.None);
        _error = result.Error;
        return result.Value;
    }

    private async Task AddAsync()
    {
        var parameters = new DialogParameters<AddBatchDialog> { { d => d.Locations, _locations } };
        _dialogOpen = true;
        var dialog = await Dialogs.ShowAsync<AddBatchDialog>(T["AddTitle"], parameters, DialogDefaults);
        var result = await dialog.Result;
        _dialogOpen = false;
        if (result is { Canceled: false, Data: InventoryActionResponse response })
        {
            MarkOwnChange();
            Undo.Show(T["Done.Added"], () => UndoAsync(response.ActivityId));
            await ReloadAsync();
        }
    }

    private async Task OpenAsync(ProductSummaryDto product)
    {
        var parameters = new DialogParameters<InventoryProductSheet> { { d => d.Product, product }, { d => d.Locations, _locations } };
        _dialogOpen = true;
        var dialog = await Dialogs.ShowAsync<InventoryProductSheet>(product.Name, parameters, DialogDefaults);
        await dialog.Result;
        _dialogOpen = false;

        // Closed by the button or the backdrop: actions in the sheet may have changed the stock either way.
        MarkOwnChange();
        await ReloadAsync();
    }

    private async Task UndoAsync(Guid activityId)
    {
        if (Session.ActiveHouseholdId is not { } household)
        {
            return;
        }

        MarkOwnChange();
        var undone = await Inventory.UndoAsync(household, activityId, CancellationToken.None);
        await InvokeAsync(async () =>
        {
            if (!undone.IsSuccess)
            {
                Snackbar.Add(T["Error.inventory.undo_not_possible"], Severity.Warning);
            }

            await ReloadAsync();
            StateHasChanged();
        });
    }

    private void MarkOwnChange() => _ownChangeUntil = Time.GetUtcNow().AddSeconds(15);

    private Task OnLiveChangedAsync(HouseholdChangesMessage message)
    {
        if (message.HouseholdId != Session.ActiveHouseholdId || !message.Changes.Any(c => RelevantEntities.Contains(c.EntityType)))
        {
            return Task.CompletedTask;
        }

        if (!_dialogOpen && Time.GetUtcNow() > _ownChangeUntil)
        {
            Announcer.Announce(T["LiveChanged"]);
        }

        var locationsChanged = message.Changes.Any(c => c.EntityType == SyncEntityTypes.StorageLocation);
        return RefreshAsync(locationsChanged);
    }

    private Task OnResyncedAsync() => RefreshAsync(locationsChanged: true);

    private Task RefreshAsync(bool locationsChanged) => InvokeAsync(async () =>
    {
        if (locationsChanged)
        {
            await LoadLocationsAsync();
        }

        await ReloadAsync();
        StateHasChanged();
    });

    private static DialogOptions DialogDefaults => new() { MaxWidth = MaxWidth.Small, FullWidth = true };

    public void Dispose()
    {
        Live.Changed -= OnLiveChangedAsync;
        Live.Resynced -= OnResyncedAsync;
    }
}
