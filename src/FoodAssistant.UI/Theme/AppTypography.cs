using System.Globalization;
using MudBlazor;

namespace FoodAssistant.UI.Theme;

/// <summary>
/// Type scale of docs/kb/design-system.md ("Schrift"). Font files are not bundled yet, so the
/// fallbacks apply until they are added (see "Offen" in the design system).
/// </summary>
public static class AppTypography
{
    /// <summary>Body font stack: Atkinson Hyperlegible Next with system fallbacks.</summary>
    public static IReadOnlyList<string> TextFont { get; } =
        ["\"Atkinson Hyperlegible Next\"", "\"Segoe UI\"", "system-ui", "sans-serif"];

    /// <summary>Heading font stack: Figtree with system fallbacks.</summary>
    public static IReadOnlyList<string> HeadingFont { get; } =
        ["\"Figtree\"", "\"Segoe UI\"", "system-ui", "sans-serif"];

    public static Typography Create()
    {
        var typography = new Typography();

        // Display, title and heading of the design system map onto h1..h3; h4..h6 reuse "heading"
        // so no size outside the scale can appear.
        Style(typography.H1, HeadingFont, 32, 38, 700);
        Style(typography.H2, HeadingFont, 24, 30, 700);
        Style(typography.H3, HeadingFont, 20, 26, 600);
        Style(typography.H4, HeadingFont, 20, 26, 600);
        Style(typography.H5, HeadingFont, 20, 26, 600);
        Style(typography.H6, HeadingFont, 20, 26, 600);

        Style(typography.Default, TextFont, 16, 24, 400);
        Style(typography.Body1, TextFont, 16, 24, 400);
        Style(typography.Subtitle1, TextFont, 18, 26, 400);
        Style(typography.Subtitle2, TextFont, 15, 20, 700);
        Style(typography.Button, TextFont, 15, 20, 700);

        // The smallest size in the app is 14 px (ADR-0009).
        Style(typography.Body2, TextFont, 14, 20, 400);
        Style(typography.Caption, TextFont, 14, 20, 400);
        Style(typography.Overline, TextFont, 14, 20, 400);
        return typography;
    }

    private static void Style(BaseTypography target, IReadOnlyList<string> family, int size, int line, int weight)
    {
        target.FontFamily = [.. family];
        target.FontSize = string.Create(CultureInfo.InvariantCulture, $"{size / 16d:0.####}rem");
        target.LineHeight = string.Create(CultureInfo.InvariantCulture, $"{line / (double)size:0.####}");
        target.FontWeight = weight.ToString(CultureInfo.InvariantCulture);
        target.LetterSpacing = "normal";
        target.TextTransform = "none";
    }
}
