using System.Text.RegularExpressions;
using FoodAssistant.UI.Tests.Support;
using FoodAssistant.UI.Theme;

namespace FoodAssistant.UI.Tests.Theme;

/// <summary>Keeps the three copies of the colour tokens in sync: design-system.md, <see cref="DesignTokens"/>, design-tokens.css.</summary>
public sealed partial class DesignTokensTests
{
    private static string Normalize(string hex) => hex.ToLowerInvariant();

    private static Dictionary<string, string> Sorted(IReadOnlyDictionary<string, string> values) =>
        values.ToDictionary(pair => pair.Key, pair => Normalize(pair.Value));

    [GeneratedRegex("(--fa-[a-z-]+)\\s*:\\s*(#[0-9a-fA-F]{6})\\s*;")]
    private static partial Regex ColourDeclaration();

    private static Dictionary<string, string> ColoursOf(string cssBlock) =>
        ColourDeclaration().Matches(cssBlock).ToDictionary(m => m.Groups[1].Value, m => Normalize(m.Groups[2].Value));

    /// <summary>The body of the first rule whose selector contains <paramref name="selector"/>.</summary>
    private static string RuleBody(string css, string selector)
    {
        var selectorStart = css.IndexOf(selector, StringComparison.Ordinal);
        Assert.True(selectorStart >= 0, $"Selector {selector} not found in design-tokens.css");
        var open = css.IndexOf('{', selectorStart);
        var close = css.IndexOf('}', open);
        return css[(open + 1)..close];
    }

    [Fact]
    public void Light_EqualsDesignSystemTable() =>
        Assert.Equal(Sorted(DesignSystemTable.Light).OrderBy(p => p.Key), Sorted(DesignTokens.Light.ToCssProperties()).OrderBy(p => p.Key));

    [Fact]
    public void Dark_EqualsDesignSystemTable() =>
        Assert.Equal(Sorted(DesignSystemTable.Dark).OrderBy(p => p.Key), Sorted(DesignTokens.Dark.ToCssProperties()).OrderBy(p => p.Key));

    [Fact]
    public void HighContrast_EqualsDesignSystemTable() =>
        Assert.Equal(Sorted(DesignSystemTable.HighContrast).OrderBy(p => p.Key), Sorted(DesignTokens.HighContrast.ToCssProperties()).OrderBy(p => p.Key));

    [Fact]
    public void Css_Light_EqualsTokens()
    {
        var css = File.ReadAllText(RepoFiles.UiPath("wwwroot/design-tokens.css"));

        Assert.Equal(Sorted(DesignTokens.Light.ToCssProperties()).OrderBy(p => p.Key), ColoursOf(RuleBody(css, ":root,")).OrderBy(p => p.Key));
    }

    [Fact]
    public void Css_Dark_Class_EqualsTokens()
    {
        var css = File.ReadAllText(RepoFiles.UiPath("wwwroot/design-tokens.css"));

        Assert.Equal(Sorted(DesignTokens.Dark.ToCssProperties()).OrderBy(p => p.Key), ColoursOf(RuleBody(css, ":root.theme-dark {")).OrderBy(p => p.Key));
    }

    [Fact]
    public void Css_Dark_SystemPreference_EqualsTokens()
    {
        var css = File.ReadAllText(RepoFiles.UiPath("wwwroot/design-tokens.css"));
        var media = css[css.IndexOf("@media (prefers-color-scheme: dark)", StringComparison.Ordinal)..];

        Assert.Equal(Sorted(DesignTokens.Dark.ToCssProperties()).OrderBy(p => p.Key), ColoursOf(RuleBody(media, ":root:not(")).OrderBy(p => p.Key));
    }

    [Fact]
    public void Css_HighContrast_EqualsTokens()
    {
        var css = File.ReadAllText(RepoFiles.UiPath("wwwroot/design-tokens.css"));

        Assert.Equal(Sorted(DesignTokens.HighContrast.ToCssProperties()).OrderBy(p => p.Key), ColoursOf(RuleBody(css, ":root.theme-high-contrast {")).OrderBy(p => p.Key));
    }

    [Fact]
    public void Css_DefinesTheSizesOfTheDesignSystem()
    {
        var css = File.ReadAllText(RepoFiles.UiPath("wwwroot/design-tokens.css"));

        foreach (var expected in new[]
                 {
                     "--fa-radius-sm: 8px", "--fa-radius-md: 12px", "--fa-radius-lg: 20px", "--fa-target: 48px",
                     "--fa-row-height: 64px", "--fa-icon: 24px", "--fa-scan-button: 64px", "--fa-space-8: 32px",
                     "\"Atkinson Hyperlegible Next\", \"Segoe UI\", system-ui, sans-serif",
                     "\"Figtree\", \"Segoe UI\", system-ui, sans-serif",
                 })
        {
            Assert.Contains(expected, css, StringComparison.Ordinal);
        }
    }
}
