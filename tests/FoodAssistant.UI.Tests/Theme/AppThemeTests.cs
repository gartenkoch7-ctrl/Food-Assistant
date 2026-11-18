using System.Globalization;
using FoodAssistant.UI.Theme;
using MudBlazor;
using MudBlazor.Utilities;

namespace FoodAssistant.UI.Tests.Theme;

public sealed class AppThemeTests
{
    public static TheoryData<ThemeVariant> Variants => new()
    {
        ThemeVariant.Light,
        ThemeVariant.Dark,
        ThemeVariant.HighContrast,
    };

    private static ThemeTokens TokensOf(ThemeVariant variant) => variant switch
    {
        ThemeVariant.Light => DesignTokens.Light,
        ThemeVariant.Dark => DesignTokens.Dark,
        _ => DesignTokens.HighContrast,
    };

    // MudColor compares by value; going through hex keeps the assertion readable and independent of alpha formatting.
    private static string Hex(MudColor color) => color.ToString(MudColorOutputFormats.Hex).ToLowerInvariant();

    [Theory]
    [MemberData(nameof(Variants))]
    public void Palette_MapsTokens_AsDefinedInTheDesignSystem(ThemeVariant variant)
    {
        var tokens = TokensOf(variant);
        var palette = AppTheme.PaletteFor(variant);

        Assert.Equal(tokens.Brand, Hex(palette.Primary));
        Assert.Equal(tokens.OnBrand, Hex(palette.PrimaryContrastText));
        Assert.Equal(tokens.Surface, Hex(palette.Background));
        Assert.Equal(tokens.SurfaceRaised, Hex(palette.Surface));
        Assert.Equal(tokens.Ink, Hex(palette.TextPrimary));
        Assert.Equal(tokens.InkMuted, Hex(palette.TextSecondary));
        Assert.Equal(tokens.Line, Hex(palette.LinesDefault));
        Assert.Equal(tokens.LineStrong, Hex(palette.LinesInputs));
        Assert.Equal(tokens.Danger, Hex(palette.Error));
        Assert.Equal(tokens.StatusSoon, Hex(palette.Warning));
        Assert.Equal(tokens.StatusOk, Hex(palette.Success));
    }

    [Fact]
    public void DefaultTheme_UsesLightAndDarkTokens()
    {
        Assert.Equal(DesignTokens.Light.Brand, Hex(AppTheme.Default.PaletteLight.Primary));
        Assert.Equal(DesignTokens.Dark.Brand, Hex(AppTheme.Default.PaletteDark.Primary));
    }

    [Fact]
    public void HighContrastTheme_HasTheSamePaletteInBothModes()
    {
        Assert.Equal(Hex(AppTheme.HighContrast.PaletteLight.Background), Hex(AppTheme.HighContrast.PaletteDark.Background));
        Assert.Equal(Hex(AppTheme.HighContrast.PaletteLight.TextPrimary), Hex(AppTheme.HighContrast.PaletteDark.TextPrimary));
        Assert.Equal(DesignTokens.HighContrast.Ink, Hex(AppTheme.HighContrast.PaletteDark.TextPrimary));
    }

    [Fact]
    public void Shape_UsesTheMediumRadius()
    {
        Assert.Equal("12px", AppTheme.Default.LayoutProperties.DefaultBorderRadius);
        Assert.Equal("12px", AppTheme.HighContrast.LayoutProperties.DefaultBorderRadius);
    }

    [Theory]
    [InlineData("\"Atkinson Hyperlegible Next\"")]
    public void BodyText_UsesAtkinsonWithSystemFallbacks(string expectedFirst)
    {
        var typography = AppTheme.Default.Typography;

        foreach (var style in new BaseTypography[] { typography.Default, typography.Body1, typography.Body2, typography.Button, typography.Caption })
        {
            var family = Assert.IsType<string[]>(style.FontFamily);
            Assert.Equal(expectedFirst, family[0]);
            Assert.Equal(["\"Segoe UI\"", "system-ui", "sans-serif"], family[1..]);
        }
    }

    [Fact]
    public void Headings_UseFigtreeWithSystemFallbacks()
    {
        var typography = AppTheme.Default.Typography;

        foreach (var style in new BaseTypography[] { typography.H1, typography.H2, typography.H3, typography.H4, typography.H5, typography.H6 })
        {
            var family = Assert.IsType<string[]>(style.FontFamily);
            Assert.Equal("\"Figtree\"", family[0]);
            Assert.Equal(["\"Segoe UI\"", "system-ui", "sans-serif"], family[1..]);
        }
    }

    [Fact]
    public void TypeScale_FollowsTheDesignSystem()
    {
        var typography = AppTheme.Default.Typography;

        Assert.Equal("2rem", typography.H1.FontSize);
        Assert.Equal("700", typography.H1.FontWeight);
        Assert.Equal("1.5rem", typography.H2.FontSize);
        Assert.Equal("1.25rem", typography.H3.FontSize);
        Assert.Equal("600", typography.H3.FontWeight);
        Assert.Equal("1.125rem", typography.Subtitle1.FontSize);
        Assert.Equal("1rem", typography.Body1.FontSize);
        Assert.Equal("1.5", typography.Body1.LineHeight);
        Assert.Equal("0.9375rem", typography.Button.FontSize);
        Assert.Equal("700", typography.Button.FontWeight);
    }

    [Fact]
    public void TypeScale_UsesDotsAsDecimalSeparator_InEveryCulture()
    {
        // CSS knows no decimal comma; a German server must not emit "0,875rem".
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");

            var typography = AppTypography.Create();

            Assert.Equal("0.875rem", typography.Caption.FontSize);
            Assert.Equal("1.4286", typography.Caption.LineHeight);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void SmallestText_IsFourteenPixels()
    {
        var typography = AppTheme.Default.Typography;

        foreach (var style in new BaseTypography[] { typography.Body2, typography.Caption, typography.Overline })
        {
            Assert.Equal("0.875rem", style.FontSize);
        }
    }
}
