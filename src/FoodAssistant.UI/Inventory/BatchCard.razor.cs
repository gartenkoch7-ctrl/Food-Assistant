using System.Globalization;
using FoodAssistant.Client.Http;
using FoodAssistant.Contracts.Catalog;
using FoodAssistant.Contracts.Inventory;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace FoodAssistant.UI.Inventory;

/// <summary>
/// One batch with its actions; shared by the product sheet and the batch page (QR target). After an
/// action the owner (<see cref="OnDone"/>) or an undo snackbar offers to undo it.
/// </summary>
public sealed partial class BatchCard
{
    private ApiError? _error;
    private bool _busy;

    [Parameter, EditorRequired]
    public BatchDto Batch { get; set; } = default!;

    [Parameter, EditorRequired]
    public ProductSummaryDto Product { get; set; } = default!;

    [Parameter, EditorRequired]
    public IReadOnlyList<StorageLocationDto> Locations { get; set; } = [];

    /// <summary>Raised after an action, a conflict or an undo; the owner reloads.</summary>
    [Parameter]
    public EventCallback Changed { get; set; }

    /// <summary>
    /// When set, the owner offers the undo (message, activity id), e.g. inside a dialog whose focus trap
    /// keeps the keyboard away from a snackbar. Otherwise an undo snackbar appears.
    /// </summary>
    [Parameter]
    public Func<string, Guid, Task>? OnDone { get; set; }

    private string Summary => string.Join(
        " · ",
        new[]
        {
            InventoryFields.Quantity(Batch.Quantity, Common, CultureInfo.CurrentCulture),
            InventoryFields.LocationName(Batch.StorageLocationId, Locations),
            InventoryFields.State(Batch.State, T),
        }.Where(part => !string.IsNullOrEmpty(part)));

    private string ETag => $"\"{Batch.Version}\"";

    private Task UsedUpAsync() => RunAsync(
        "UsedUp",
        household => Inventory.ConsumeBatchAsync(household, Batch.Id, new ConsumeBatchRequest(ConsumptionKindDto.UsedUp, null, null), ETag, CancellationToken.None));

    private Task OpenAsync() => RunAsync("Opened", household => Inventory.OpenBatchAsync(household, Batch.Id, ETag, CancellationToken.None));

    private Task ConsumeAsync(ConsumptionKindDto mode) => DialogAsync<ConsumeDialog>(
        mode == ConsumptionKindDto.Partial ? "Partial" : "Discarded",
        new DialogParameters<ConsumeDialog> { { d => d.Batch, Batch }, { d => d.Mode, mode } });

    private Task MoveAsync() => DialogAsync<MoveBatchDialog>(
        "Moved", new DialogParameters<MoveBatchDialog> { { d => d.Batch, Batch }, { d => d.Locations, Locations } });

    private Task CorrectAsync() => DialogAsync<CorrectBatchDialog>(
        "Corrected", new DialogParameters<CorrectBatchDialog> { { d => d.Batch, Batch } });

    private Task LeftoverAsync() => DialogAsync<LeftoverDialog>(
        "Leftover",
        new DialogParameters<LeftoverDialog> { { d => d.Product, Product }, { d => d.Locations, Locations }, { d => d.LocationId, Batch.StorageLocationId } });

    private async Task DialogAsync<TDialog>(string done, DialogParameters<TDialog> parameters)
        where TDialog : ComponentBase
    {
        _error = null;
        var dialog = await Dialogs.ShowAsync<TDialog>(string.Empty, parameters, new DialogOptions { MaxWidth = MaxWidth.Small, FullWidth = true });
        var result = await dialog.Result;
        if (result is { Canceled: false, Data: InventoryActionResponse response })
        {
            await DoneAsync(done, response);
        }
        else if (result is { Canceled: false, Data: ApiError conflict })
        {
            await ConflictAsync(conflict);
        }
    }

    private async Task RunAsync(string done, Func<Guid, Task<ApiResult<InventoryActionResponse>>> action)
    {
        if (Session.ActiveHouseholdId is not { } household)
        {
            return;
        }

        _busy = true;
        _error = null;
        var result = await action(household);
        _busy = false;
        if (result.IsSuccess)
        {
            await DoneAsync(done, result.Value!);
        }
        else if (result.Error!.Status is 412 or 404)
        {
            await ConflictAsync(result.Error);
        }
        else
        {
            _error = result.Error;
        }
    }

    // Someone else changed (412) or removed (404) the batch: reload and say so; a next try uses the new state.
    private Task ConflictAsync(ApiError conflict)
    {
        _error = conflict;
        return Changed.InvokeAsync();
    }

    private async Task DoneAsync(string done, InventoryActionResponse response)
    {
        if (OnDone is not null)
        {
            await OnDone(T["Done." + done], response.ActivityId);
            await Changed.InvokeAsync();
            return;
        }

        var household = Session.ActiveHouseholdId;
        var changed = Changed;
        Undo.Show(T["Done." + done], async () =>
        {
            // The card may be gone after the reload; everything needed is captured here.
            var undone = household is { } id ? await Inventory.UndoAsync(id, response.ActivityId, CancellationToken.None) : null;
            if (undone is not { IsSuccess: true })
            {
                Snackbar.Add(T["Error.inventory.undo_not_possible"], Severity.Warning);
            }

            await changed.InvokeAsync();
        });
        await Changed.InvokeAsync();
    }
}
