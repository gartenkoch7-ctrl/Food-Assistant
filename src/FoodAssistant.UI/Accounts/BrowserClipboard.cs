using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace FoodAssistant.UI.Accounts;

/// <summary><c>navigator.clipboard.writeText</c> through JS interop; the returned promise is awaited by the interop.</summary>
public sealed class BrowserClipboard(IJSRuntime js, ILogger<BrowserClipboard> logger) : IClipboard
{
    public async Task<bool> WriteTextAsync(string text, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(text);
        try
        {
            await js.InvokeVoidAsync("navigator.clipboard.writeText", cancellationToken, text).ConfigureAwait(false);
            return true;
        }
        catch (Exception ex) when (ex is JSException or InvalidOperationException or JSDisconnectedException or OperationCanceledException)
        {
            // Permission denied, insecure context or no clipboard: the page shows the link for copying by hand.
            AccountsLog.ClipboardUnavailable(logger, ex);
            return false;
        }
    }
}
