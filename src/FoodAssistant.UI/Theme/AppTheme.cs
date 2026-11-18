using MudBlazor;

namespace FoodAssistant.UI.Theme;

/// <summary>
/// MudBlazor themes derived from the design tokens (docs/kb/design-system.md). The mapping follows the
/// design system's "Umsetzung in MudBlazor" table: Primary = brand, Background = surface, Surface = surface-raised,
/// TextPrimary = ink, TextSecondary = ink-muted, LinesDefault = line, LinesInputs = line-strong,
/// Error = danger, Warning = status-soon, Success = status-ok.
/// </summary>
public static class AppTheme
{
    private const string Radius = "12px";

    /// <summary>Light and dark palettes; MudThemeProvider switches between them via <c>IsDarkMode</c>.</summary>
    public static MudTheme Default { get; } = Create(DesignTokens.Light, DesignTokens.Dark);

    /// <summary>High-contrast theme: the same palette in both modes, so the dark-mode switch has no effect.</summary>
    public static MudTheme HighContrast { get; } = Create(DesignTokens.HighContrast, DesignTokens.HighContrast);

    /// <summary>Palette of the given theme, for tests and tools that need the effective colours.</summary>
    public static Palette PaletteFor(ThemeVariant variant) => variant switch
    {
        ThemeVariant.Light => Default.PaletteLight,
        ThemeVariant.Dark => Default.PaletteDark,
        _ => HighContrast.PaletteLight,
    };

    private static MudTheme Create(ThemeTokens light, ThemeTokens dark) => new()
    {
        PaletteLight = Apply(new PaletteLight(), light),
        PaletteDark = Apply(new PaletteDark(), dark),
        LayoutProperties = new LayoutProperties { DefaultBorderRadius = Radius },
        Typography = AppTypography.Create(),
    };

    private static TPalette Apply<TPalette>(TPalette palette, ThemeTokens t)
        where TPalette : Palette
    {
        palette.Primary = t.Brand;
        palette.PrimaryContrastText = t.OnBrand;
        palette.PrimaryDarken = t.BrandStrong;
        palette.PrimaryLighten = t.BrandTint;

        // Secondary and tertiary stay neutral: the brand colour is used sparingly (ADR-0009).
        palette.Secondary = t.InkMuted;
        palette.SecondaryContrastText = t.Surface;
        palette.Tertiary = t.InkMuted;
        palette.TertiaryContrastText = t.Surface;
        palette.Dark = t.Ink;
        palette.DarkContrastText = t.Surface;

        palette.Info = t.Focus;
        palette.InfoContrastText = t.OnBrand;
        palette.Error = t.Danger;
        palette.ErrorContrastText = t.StatusExpiredBg;
        palette.Warning = t.StatusSoon;
        palette.WarningContrastText = t.StatusSoonBg;
        palette.Success = t.StatusOk;
        palette.SuccessContrastText = t.StatusOkBg;

        palette.Background = t.Surface;
        palette.BackgroundGray = t.SurfaceSunken;
        palette.Surface = t.SurfaceRaised;
        palette.DrawerBackground = t.SurfaceRaised;
        palette.DrawerText = t.Ink;
        palette.DrawerIcon = t.InkMuted;
        palette.AppbarBackground = t.SurfaceRaised;
        palette.AppbarText = t.Ink;

        palette.TextPrimary = t.Ink;
        palette.TextSecondary = t.InkMuted;
        palette.ActionDefault = t.InkMuted;

        palette.LinesDefault = t.Line;
        palette.LinesInputs = t.LineStrong;
        palette.Divider = t.Line;
        palette.DividerLight = t.Line;
        palette.TableLines = t.Line;
        return palette;
    }
}
