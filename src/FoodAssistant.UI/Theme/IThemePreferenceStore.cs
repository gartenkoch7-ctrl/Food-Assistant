namespace FoodAssistant.UI.Theme;

/// <summary>Remembers the chosen <see cref="ThemeMode"/> on this device. Failures must not surface: the default applies.</summary>
public interface IThemePreferenceStore
{
    Task<ThemeMode> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(ThemeMode mode, CancellationToken cancellationToken = default);
}
