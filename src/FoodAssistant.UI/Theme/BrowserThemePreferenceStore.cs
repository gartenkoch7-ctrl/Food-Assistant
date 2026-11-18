using FoodAssistant.UI.Common;

namespace FoodAssistant.UI.Theme;

/// <summary>Keeps the theme mode in the browser's local storage; unavailable storage means "System".</summary>
public sealed class BrowserThemePreferenceStore(IBrowserInterop browser) : IThemePreferenceStore
{
    /// <summary>Also read by <c>wwwroot/foodassistant.js</c> to avoid a flash of the wrong theme.</summary>
    public const string StorageKey = "foodassistant.theme";

    public async Task<ThemeMode> LoadAsync(CancellationToken cancellationToken = default) =>
        ThemeModeTokens.ParseToken(await browser.GetItemAsync(StorageKey, cancellationToken).ConfigureAwait(false));

    public Task SaveAsync(ThemeMode mode, CancellationToken cancellationToken = default) =>
        browser.SetItemAsync(StorageKey, mode.ToToken(), cancellationToken);
}
