using System.Globalization;
using System.Text.Json;
using FoodAssistant.Client.Http;
using FoodAssistant.Contracts.Catalog;
using FoodAssistant.Contracts.Checkout;
using FoodAssistant.Contracts.Common;
using FoodAssistant.Contracts.Inventory;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace FoodAssistant.UI.Checkout;

/// <summary>
/// "Einkauf abschließen": every checked entry of a list with suggested amount, location and date;
/// free text is assigned a product or skipped; one request books everything (docs/kb/einkauf.md).
/// </summary>
public sealed partial class CheckoutPage
{
    private List<CheckoutLine> _lines = [];
    private IReadOnlyList<StorageLocationDto> _locations = [];
    private Dictionary<string, StorageTypeDto> _categoryTypes = [];
    private Dictionary<int, string> _rowErrors = [];
    private ApiError? _error;
    private Guid _loadedFor;
    private bool _loading = true;
    private bool _loaded;
    private bool _saving;
    private bool _checkLines;
    private int _staleCount;

    // The key stays the same while the same request is repeated after a failure without answer, so
    // the server can replay its answer instead of booking twice; any other request gets a new key.
    private Guid _idempotencyKey;
    private string? _unansweredBody;

    [Parameter]
    public Guid ListId { get; set; }

    private string ListUrl => $"/shopping/{ListId:D}";

    private int BookCount => _lines.Count(l => !l.Skip);

    protected override async Task OnParametersSetAsync()
    {
        if (_loadedFor == ListId)
        {
            return;
        }

        _loadedFor = ListId;
        (_loading, _staleCount, _unansweredBody) = (true, 0, null);
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        if (!Session.CanEdit || Session.ActiveHouseholdId is not { } household)
        {
            _loading = false;
            return;
        }

        var locations = await Catalog.ListStorageLocationsAsync(household, CancellationToken.None);
        var categories = await Catalog.ListCategoriesAsync(household, CancellationToken.None);
        var proposal = await Checkout.GetProposalAsync(household, ListId, CancellationToken.None);
        _locations = locations.Value ?? _locations;

        // Without categories an assigned product simply keeps the suggested location.
        _categoryTypes = categories.Value?.ToDictionary(c => c.Key, c => c.DefaultStorageType) ?? _categoryTypes;
        _error = proposal.Error ?? locations.Error;
        _loaded = proposal.IsSuccess;
        _lines = proposal.Value is { } loaded ? [.. loaded.Entries.Select(e => new CheckoutLine(e))] : [];
        (_rowErrors, _checkLines, _loading) = ([], false, false);
    }

    private void OnLineChanged(CheckoutLine line) => _rowErrors.Remove(_lines.IndexOf(line));

    private async Task EditAsync(CheckoutLine line)
    {
        var parameters = new DialogParameters<CheckoutLineEditor>
        {
            { d => d.Line, line },
            { d => d.Locations, _locations },
        };
        var dialog = await Dialogs.ShowAsync<CheckoutLineEditor>(
            line.Name, parameters, new DialogOptions { MaxWidth = MaxWidth.Small, FullWidth = true });
        if (await dialog.Result is { Canceled: false, Data: CheckoutLine edited })
        {
            line.Apply(edited);
            OnLineChanged(line);
        }
    }

    private async Task SubmitAsync()
    {
        if (_saving || Session.ActiveHouseholdId is not { } household)
        {
            return;
        }

        _rowErrors = CheckoutErrors.Missing(_lines, T, Inventory);
        _checkLines = _rowErrors.Count > 0;
        if (_checkLines)
        {
            return;
        }

        var request = new CompleteCheckoutRequest([.. _lines.Select(l => l.ToRequest())]);
        var body = JsonSerializer.Serialize(request);
        if (body != _unansweredBody)
        {
            (_idempotencyKey, _unansweredBody) = (Guid.CreateVersion7(), body);
        }

        _saving = true;
        _error = null;
        var result = await Checkout.CompleteAsync(household, ListId, request, _idempotencyKey, CancellationToken.None);
        _saving = false;
        // No answer yet (network, offline, or the first attempt still running on the server): the outcome
        // is unknown, so the same request must go out with the same key again.
        if (result.Error is { ErrorCode: ClientErrorCodes.Network or ClientErrorCodes.Offline or ErrorCodes.IdempotencyRequestInProgress } unanswered)
        {
            _error = unanswered;
            return;
        }

        _unansweredBody = null;
        if (result.Error is { ErrorCode: ErrorCodes.Conflict } conflict)
        {
            // A location was deleted or switched between fridge and freezer: the suggestions (rule dates,
            // last purchases) were made for the old places, so everything is loaded again.
            await LoadAsync();
            _error = conflict;
            return;
        }

        if (result.Value is not { } done)
        {
            _error = result.Error;
            _rowErrors = CheckoutErrors.FromServer(result.Error, _lines.Count, T, Inventory);

            // A product archived meanwhile: the line offers the product picker again instead of only "skip".
            foreach (var index in CheckoutErrors.LinesWithField(result.Error, "productId", _lines.Count))
            {
                (_lines[index].ProductId, _lines[index].Name) = (null, _lines[index].Text);
            }

            return;
        }

        var message = done.BatchesCreated switch
        {
            0 => string.Format(CultureInfo.CurrentCulture, T["DoneNone"].Value, done.ItemsRemoved),
            1 => T["DoneOne"].Value,
            _ => string.Format(CultureInfo.CurrentCulture, T["DoneMany"].Value, done.BatchesCreated),
        };
        Announcer.Announce(message);
        Snackbar.Add(message, Severity.Success);
        if (done.StaleItemIds.Count == 0)
        {
            Navigation.NavigateTo(ListUrl);
            return;
        }

        // The changed entries come back with their current state; the rest is gone from the list.
        await LoadAsync();
        _staleCount = done.StaleItemIds.Count;
    }
}
