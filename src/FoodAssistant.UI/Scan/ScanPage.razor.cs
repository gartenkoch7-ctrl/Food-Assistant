using System.Globalization;
using FoodAssistant.Client.Scan;
using FoodAssistant.Contracts.Catalog;
using FoodAssistant.UI.Common;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

namespace FoodAssistant.UI.Scan;

/// <summary>
/// Scan page: camera and manual entry feed the same flow (<see cref="ScanFlow"/>), so scanning is
/// never the only way in (ADR-0008). The camera runs only after the button is pressed and stops
/// when the page is left.
/// </summary>
public sealed partial class ScanPage
{
    private const string VideoId = "scan-video";
    private const string BarcodeInputId = "scan-barcode-input";

    private IReadOnlyList<ChoiceOption<ScanMode>> _modes = [];
    private ScanMode _mode = ScanMode.Add;
    private string? _manual;
    private bool? _cameraAvailable;
    private bool _cameraFailed;
    private bool _scanning;
    private bool _busy;
    private bool _starting;
    private int _pickerKey;
    private ScanOutcome? _outcome;

    [Inject]
    private IStringLocalizer<ScanText> T { get; set; } = default!;

    [Inject]
    private ScanFlow Flow { get; set; } = default!;

    [Inject]
    private IBarcodeScanner Scanner { get; set; } = default!;

    [Inject]
    private IBrowserInterop Browser { get; set; } = default!;

    [Inject]
    private ILiveAnnouncer Announcer { get; set; } = default!;

    [Inject]
    private NavigationManager Navigation { get; set; } = default!;

    protected override void OnInitialized() =>
        _modes =
        [
            new(ScanMode.Add, T["ModeAdd"]),
            new(ScanMode.Use, T["ModeUse"]),
        ];

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            _cameraAvailable = await Scanner.IsAvailableAsync(CancellationToken.None);
            StateHasChanged();
        }
    }

    private void SetMode(ScanMode mode) => _mode = mode;

    private async Task ToggleCameraAsync()
    {
        if (_scanning)
        {
            _scanning = false;
            await Scanner.StopAsync();
            return;
        }

        // The permission prompt can stay open for a long time; a second press must not start a second stream.
        _cameraFailed = false;
        _starting = true;
        try
        {
            _scanning = await Scanner.StartAsync(VideoId, OnCodeAsync, CancellationToken.None);
        }
        finally
        {
            _starting = false;
        }

        _cameraFailed = !_scanning;
    }

    // Called by the camera; a code that arrives while another one is handled is dropped.
    private Task OnCodeAsync(string raw) => InvokeAsync(() => HandleRawAsync(raw, fromCamera: true));

    private Task SubmitAsync() => HandleRawAsync(_manual, fromCamera: false);

    private Task HandleRawAsync(string? raw, bool fromCamera)
    {
        var code = ScanCodeParser.Parse(raw, new Uri(Navigation.BaseUri));
        return RunAsync(() => Flow.HandleAsync(code, _mode, CancellationToken.None), clearField: !fromCamera && code.Kind != ScanCodeKind.Unknown, focusField: !fromCamera);
    }

    private Task ProductChosenAsync(ProductSummaryDto product) =>
        RunAsync(() => Flow.HandleProductAsync(product.Id, _mode, CancellationToken.None), clearField: false);

    private Task UndoAsync() => RunAsync(() => Flow.UndoAsync(_outcome!, CancellationToken.None), clearField: false);

    private Task ChangeAmountAsync() => RunAsync(() => Flow.ChangeAmountAsync(_outcome!, CancellationToken.None), clearField: false);

    private async Task RunAsync(Func<Task<ScanOutcome>> action, bool clearField, bool focusField = true)
    {
        if (_busy)
        {
            return;
        }

        _busy = true;
        StateHasChanged();
        try
        {
            _outcome = await action();
        }
        finally
        {
            _busy = false;
        }

        if (clearField)
        {
            _manual = null;
        }

        _pickerKey++;
        if (_outcome.Kind != ScanOutcomeKind.Failed)
        {
            // Errors are announced by the alert (role=alert); everything else goes to the polite live region.
            Announcer.Announce(MessageOf(_outcome));
        }

        StateHasChanged();

        // Dialogs took the focus; the next barcode is typed or scanned here.
        if (focusField)
        {
            await Browser.FocusElementAsync(BarcodeInputId);
        }
    }

    private string MessageOf(ScanOutcome outcome)
    {
        var name = outcome.ProductName ?? string.Empty;
        return outcome.Kind switch
        {
            ScanOutcomeKind.Added => Format(T["DoneAdded"], name),
            ScanOutcomeKind.Used => Format(T["DoneUsed"], name),
            ScanOutcomeKind.Undone => T["DoneUndone"],
            ScanOutcomeKind.NotInStock => Format(T["NotInStock"], name),
            ScanOutcomeKind.UnknownProduct => T["UnknownProduct"],
            ScanOutcomeKind.UnknownCode => T["UnknownCode"],
            ScanOutcomeKind.Navigated => T["Navigated"],
            ScanOutcomeKind.ProductCreated => Format(T["ProductCreated"], name),
            ScanOutcomeKind.Cancelled => T["Cancelled"],
            _ => string.Empty,
        };
    }

    private static string Format(LocalizedString text, string name) =>
        string.Format(CultureInfo.CurrentCulture, text.Value, name);

    public async ValueTask DisposeAsync() => await Scanner.StopAsync();
}
