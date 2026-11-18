using FoodAssistant.Client.Scan;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace FoodAssistant.UI.Scan;

/// <summary>
/// <see cref="IBarcodeScanner"/> over the browser camera and <c>BarcodeDetector</c>
/// (<c>wwwroot/scan.js</c>). The module is loaded on first use, so prerendering and hosts without a
/// browser never touch it; every JS failure means "no scanning" and the page offers manual entry.
/// </summary>
internal sealed class BrowserBarcodeScanner(IJSRuntime js, ILogger<BrowserBarcodeScanner> logger) : IBarcodeScanner, IAsyncDisposable
{
    private const string ModulePath = "./_content/FoodAssistant.UI/scan.js";

    private IJSObjectReference? _module;
    private DotNetObjectReference<CodeReceiver>? _receiver;

    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken)
    {
        try
        {
            var module = await ModuleAsync(cancellationToken).ConfigureAwait(false);
            return await module.InvokeAsync<bool>("isAvailable", cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (IsUnavailable(ex))
        {
            var reason = ex.GetType().Name;
            ScanLog.CameraUnavailable(logger, reason);
            return false;
        }
    }

    public async Task<bool> StartAsync(string videoElementId, Func<string, Task> onCode, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(onCode);
        try
        {
            var module = await ModuleAsync(cancellationToken).ConfigureAwait(false);
            _receiver?.Dispose();
            _receiver = DotNetObjectReference.Create(new CodeReceiver(onCode));
            var started = await module.InvokeAsync<bool>("start", cancellationToken, videoElementId, _receiver).ConfigureAwait(false);
            if (!started)
            {
                ScanLog.CameraUnavailable(logger, "NotStarted");
            }

            return started;
        }
        catch (Exception ex) when (IsUnavailable(ex))
        {
            var reason = ex.GetType().Name;
            ScanLog.CameraUnavailable(logger, reason);
            return false;
        }
    }

    public async Task StopAsync()
    {
        try
        {
            if (_module is not null)
            {
                await _module.InvokeVoidAsync("stop").ConfigureAwait(false);
            }
        }
        catch (Exception ex) when (IsUnavailable(ex))
        {
            // The page is gone; the browser releases the camera with it.
        }

        _receiver?.Dispose();
        _receiver = null;
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync().ConfigureAwait(false);
        if (_module is null)
        {
            return;
        }

        try
        {
            await _module.DisposeAsync().ConfigureAwait(false);
        }
        catch (Exception ex) when (IsUnavailable(ex))
        {
            // Circuit already disconnected.
        }

        _module = null;
    }

    private async ValueTask<IJSObjectReference> ModuleAsync(CancellationToken cancellationToken) =>
        _module ??= await js.InvokeAsync<IJSObjectReference>("import", cancellationToken, ModulePath).ConfigureAwait(false);

    // InvalidOperationException: JS interop during prerendering. JSException: the browser refused.
    private static bool IsUnavailable(Exception exception) =>
        exception is JSException or InvalidOperationException or JSDisconnectedException or OperationCanceledException;

    /// <summary>Target of the JS callback; forwards decoded codes to the page.</summary>
    public sealed class CodeReceiver(Func<string, Task> onCode)
    {
        [JSInvokable("OnCode")]
        public Task OnCode(string text) => onCode(text);
    }
}
