namespace FoodAssistant.UI.Theme;

/// <summary>Token values of the three themes, copied from docs/kb/design-system.md.</summary>
public static class DesignTokens
{
    public static ThemeTokens Light { get; } = new(
        Surface: "#fbf9f7",
        SurfaceRaised: "#ffffff",
        SurfaceSunken: "#f2eeea",
        Line: "#e2dbd4",
        LineStrong: "#8c8178",
        Ink: "#221c18",
        InkMuted: "#6b615a",
        Brand: "#c0391f",
        BrandStrong: "#a12f18",
        BrandTint: "#fbe6e0",
        OnBrand: "#ffffff",
        Focus: "#1c5fd1",
        StatusExpired: "#8a0f3c",
        StatusExpiredBg: "#fbe4ec",
        StatusSoon: "#7a4b00",
        StatusSoonBg: "#fff1d6",
        StatusOk: "#1e5e2e",
        StatusOkBg: "#e3f2e6",
        StatusNone: "#6b615a",
        StatusNoneBg: "#f2eeea",
        Danger: "#8a0f3c");

    public static ThemeTokens Dark { get; } = new(
        Surface: "#171412",
        SurfaceRaised: "#211d1a",
        SurfaceSunken: "#110f0d",
        Line: "#3a332e",
        LineStrong: "#857a71",
        Ink: "#f4efea",
        InkMuted: "#b3a89f",
        Brand: "#ff7a5c",
        BrandStrong: "#ff9a82",
        BrandTint: "#5a2416",
        OnBrand: "#1a1513",
        Focus: "#8ab4ff",
        StatusExpired: "#ff9fc0",
        StatusExpiredBg: "#4a0f25",
        StatusSoon: "#ffcb66",
        StatusSoonBg: "#3d2a00",
        StatusOk: "#8edba2",
        StatusOkBg: "#12331c",
        StatusNone: "#b3a89f",
        StatusNoneBg: "#2a2521",
        Danger: "#ff9fc0");

    public static ThemeTokens HighContrast { get; } = new(
        Surface: "#ffffff",
        SurfaceRaised: "#ffffff",
        SurfaceSunken: "#ffffff",
        Line: "#000000",
        LineStrong: "#000000",
        Ink: "#000000",
        InkMuted: "#2b2b2b",
        Brand: "#9e2a12",
        BrandStrong: "#7a1f0b",
        BrandTint: "#ffffff",
        OnBrand: "#ffffff",
        Focus: "#0038a8",
        StatusExpired: "#6b0a2e",
        StatusExpiredBg: "#ffffff",
        StatusSoon: "#5c3800",
        StatusSoonBg: "#ffffff",
        StatusOk: "#0f4420",
        StatusOkBg: "#ffffff",
        StatusNone: "#2b2b2b",
        StatusNoneBg: "#ffffff",
        Danger: "#6b0a2e");
}
