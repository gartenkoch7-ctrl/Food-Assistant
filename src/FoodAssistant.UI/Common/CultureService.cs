using System.Globalization;

namespace FoodAssistant.UI.Common;

public sealed class CultureService(IBrowserInterop browser) : ICultureService
{
    public const string StorageKey = "foodassistant.language";

    public string Current => SupportedCultures.Resolve(CultureInfo.CurrentUICulture.Name);

    public async Task<string> ResolveStoredAsync(CancellationToken cancellationToken = default)
    {
        // In WebAssembly the runtime already starts with the browser's language.
        var stored = await browser.GetItemAsync(StorageKey, cancellationToken).ConfigureAwait(false);
        return SupportedCultures.Resolve(stored ?? CultureInfo.CurrentUICulture.Name);
    }

    public void Apply(string culture)
    {
        var info = CultureInfo.GetCultureInfo(SupportedCultures.Resolve(culture));

        // The defaults reach every thread and later render; the current values cover the caller's context.
        CultureInfo.DefaultThreadCurrentCulture = info;
        CultureInfo.DefaultThreadCurrentUICulture = info;
        CultureInfo.CurrentCulture = info;
        CultureInfo.CurrentUICulture = info;
    }

    public async Task SetAsync(string culture, CancellationToken cancellationToken = default)
    {
        var resolved = SupportedCultures.Resolve(culture);
        Apply(resolved);
        await browser.SetItemAsync(StorageKey, resolved, cancellationToken).ConfigureAwait(false);
        await browser.SetCultureCookieAsync(resolved, cancellationToken).ConfigureAwait(false);
    }
}
