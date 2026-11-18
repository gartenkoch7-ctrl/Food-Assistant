namespace FoodAssistant.UI.Theme;

/// <summary>
/// The theme mode of the current session. <c>ThemeHost</c> reads it, the "More" page changes it.
/// Scoped: one instance per browser tab (WebAssembly) or per prerender request.
/// </summary>
public sealed class ThemeState(IThemePreferenceStore store)
{
    private bool _loaded;
    private int _version;

    public ThemeMode Mode { get; private set; } = ThemeMode.System;

    /// <summary>Raised after <see cref="Mode"/> changed, including the first load from the store.</summary>
    public event Action? Changed;

    /// <summary>Loads the stored mode once; later calls do nothing.</summary>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_loaded)
        {
            return;
        }

        _loaded = true;
        var versionBeforeLoad = _version;
        var stored = await store.LoadAsync(cancellationToken).ConfigureAwait(false);

        // A choice made while the stored value was loading is newer and must win.
        if (_version == versionBeforeLoad && stored != Mode)
        {
            Mode = stored;
            Changed?.Invoke();
        }
    }

    public async Task SetModeAsync(ThemeMode mode, CancellationToken cancellationToken = default)
    {
        _loaded = true;
        if (mode == Mode)
        {
            return;
        }

        Mode = mode;
        _version++;
        Changed?.Invoke();
        await store.SaveAsync(mode, cancellationToken).ConfigureAwait(false);
    }
}
