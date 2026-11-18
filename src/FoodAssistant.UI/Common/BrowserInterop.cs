using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace FoodAssistant.UI.Common;

/// <summary>JS-interop implementation of <see cref="IBrowserInterop"/>; the script is <c>wwwroot/foodassistant.js</c>.</summary>
public sealed class BrowserInterop(IJSRuntime js, ILogger<BrowserInterop> logger) : IBrowserInterop
{
    public async Task<string?> GetItemAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            return await js.InvokeAsync<string?>("localStorage.getItem", cancellationToken, key).ConfigureAwait(false);
        }
        catch (Exception ex) when (IsUnavailable(ex))
        {
            CommonLog.BrowserCallUnavailable(logger, ex, "getItem");
            return null;
        }
    }

    public Task SetItemAsync(string key, string value, CancellationToken cancellationToken = default) =>
        InvokeVoidAsync("localStorage.setItem", cancellationToken, key, value);

    public Task ApplyThemeAsync(string themeToken, CancellationToken cancellationToken = default) =>
        InvokeVoidAsync("foodAssistant.applyTheme", cancellationToken, themeToken);

    public Task FocusElementAsync(string elementId, CancellationToken cancellationToken = default) =>
        InvokeVoidAsync("foodAssistant.focusElement", cancellationToken, elementId);

    public Task SetCultureCookieAsync(string culture, CancellationToken cancellationToken = default) =>
        InvokeVoidAsync("foodAssistant.setCultureCookie", cancellationToken, culture);

    private async Task InvokeVoidAsync(string identifier, CancellationToken cancellationToken, params object?[] args)
    {
        try
        {
            await js.InvokeVoidAsync(identifier, cancellationToken, args).ConfigureAwait(false);
        }
        catch (Exception ex) when (IsUnavailable(ex))
        {
            CommonLog.BrowserCallUnavailable(logger, ex, identifier);
        }
    }

    // InvalidOperationException: JS interop during prerendering. JSException: storage blocked or
    // quota exceeded. The others: the circuit or page went away while the call was in flight.
    private static bool IsUnavailable(Exception exception) =>
        exception is JSException or InvalidOperationException or JSDisconnectedException or OperationCanceledException;
}
