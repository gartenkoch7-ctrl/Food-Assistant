namespace FoodAssistant.UI.Theme;

/// <summary>
/// One complete set of design tokens (docs/kb/design-system.md, "Farben"). The same values live in
/// <c>wwwroot/design-tokens.css</c> as custom properties; a test keeps both in sync.
/// </summary>
public sealed record ThemeTokens(
    string Surface,
    string SurfaceRaised,
    string SurfaceSunken,
    string Line,
    string LineStrong,
    string Ink,
    string InkMuted,
    string Brand,
    string BrandStrong,
    string BrandTint,
    string OnBrand,
    string Focus,
    string StatusExpired,
    string StatusExpiredBg,
    string StatusSoon,
    string StatusSoonBg,
    string StatusOk,
    string StatusOkBg,
    string StatusNone,
    string StatusNoneBg,
    string Danger)
{
    /// <summary>Prefix of every custom property in <c>design-tokens.css</c>.</summary>
    public const string CssPrefix = "--fa-";

    /// <summary>The tokens as CSS custom properties (<c>--fa-surface-raised</c> and so on).</summary>
    public IReadOnlyDictionary<string, string> ToCssProperties() => new Dictionary<string, string>
    {
        [CssPrefix + "surface"] = Surface,
        [CssPrefix + "surface-raised"] = SurfaceRaised,
        [CssPrefix + "surface-sunken"] = SurfaceSunken,
        [CssPrefix + "line"] = Line,
        [CssPrefix + "line-strong"] = LineStrong,
        [CssPrefix + "ink"] = Ink,
        [CssPrefix + "ink-muted"] = InkMuted,
        [CssPrefix + "brand"] = Brand,
        [CssPrefix + "brand-strong"] = BrandStrong,
        [CssPrefix + "brand-tint"] = BrandTint,
        [CssPrefix + "on-brand"] = OnBrand,
        [CssPrefix + "focus"] = Focus,
        [CssPrefix + "status-expired"] = StatusExpired,
        [CssPrefix + "status-expired-bg"] = StatusExpiredBg,
        [CssPrefix + "status-soon"] = StatusSoon,
        [CssPrefix + "status-soon-bg"] = StatusSoonBg,
        [CssPrefix + "status-ok"] = StatusOk,
        [CssPrefix + "status-ok-bg"] = StatusOkBg,
        [CssPrefix + "status-none"] = StatusNone,
        [CssPrefix + "status-none-bg"] = StatusNoneBg,
        [CssPrefix + "danger"] = Danger,
    };
}
