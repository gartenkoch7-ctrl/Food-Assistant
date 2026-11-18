namespace FoodAssistant.UI.Theme;

/// <summary>
/// The stable text form of <see cref="ThemeMode"/>, used in local storage and as CSS class suffix.
/// <c>wwwroot/foodassistant.js</c> reads the same values before the first paint.
/// </summary>
public static class ThemeModeTokens
{
    public static string ToToken(this ThemeMode mode) => mode switch
    {
        ThemeMode.Light => "light",
        ThemeMode.Dark => "dark",
        ThemeMode.HighContrast => "high-contrast",
        _ => "system",
    };

    /// <summary>Unknown or missing values fall back to <see cref="ThemeMode.System"/>.</summary>
    public static ThemeMode ParseToken(string? token) => token switch
    {
        "light" => ThemeMode.Light,
        "dark" => ThemeMode.Dark,
        "high-contrast" => ThemeMode.HighContrast,
        _ => ThemeMode.System,
    };
}
