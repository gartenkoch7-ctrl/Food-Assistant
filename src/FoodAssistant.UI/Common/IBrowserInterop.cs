namespace FoodAssistant.UI.Common;

/// <summary>
/// Everything the UI needs from the browser outside Blazor: local storage, the theme class on
/// the document, the culture cookie. All calls tolerate a missing browser (prerendering, blocked
/// storage, private mode): reads return <c>null</c>, writes do nothing.
/// </summary>
public interface IBrowserInterop
{
    Task<string?> GetItemAsync(string key, CancellationToken cancellationToken = default);

    Task SetItemAsync(string key, string value, CancellationToken cancellationToken = default);

    /// <summary>Sets the theme class (<c>theme-light</c> and so on) on the document element; <c>system</c> removes it.</summary>
    Task ApplyThemeAsync(string themeToken, CancellationToken cancellationToken = default);

    /// <summary>Moves keyboard focus to the element with the given id (skip link).</summary>
    Task FocusElementAsync(string elementId, CancellationToken cancellationToken = default);

    /// <summary>Writes the culture cookie so the server prerenders in the same language.</summary>
    Task SetCultureCookieAsync(string culture, CancellationToken cancellationToken = default);
}
