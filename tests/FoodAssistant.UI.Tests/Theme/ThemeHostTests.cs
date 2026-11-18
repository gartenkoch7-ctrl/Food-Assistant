using Microsoft.Extensions.DependencyInjection;
using Bunit;
using FoodAssistant.UI.Theme;
using FoodAssistant.UI.Tests.Support;
using Microsoft.AspNetCore.Components;

namespace FoodAssistant.UI.Tests.Theme;

public sealed class ThemeHostTests : UiTestContext
{
    private IRenderedComponent<ThemeHost> RenderHost() =>
        Render<ThemeHost>(p => p.Add(
            c => c.ChildContent,
            (RenderFragment)(builder => builder.AddMarkupContent(0, "<p id=\"child\">Inhalt</p>"))));

    private static string ProviderStyle(IRenderedComponent<ThemeHost> cut) => cut.Find("style.mud-theme-provider").TextContent;

    [Fact]
    public void Renders_ItsContent()
    {
        var cut = RenderHost();

        Assert.Equal("Inhalt", cut.Find("#child").TextContent);
    }

    [Fact]
    public void Default_AppliesTheSystemMode_AndLightPalette()
    {
        var cut = RenderHost();

        cut.WaitForAssertion(() => Assert.Contains("system", Browser.AppliedThemes));
        Assert.Contains("--mud-palette-primary: rgba(192,57,31,1)", ProviderStyle(cut), StringComparison.Ordinal);
    }

    [Fact]
    public void StoredDark_UsesTheDarkPalette_AndTellsTheDocument()
    {
        Browser.Storage[BrowserThemePreferenceStore.StorageKey] = "dark";

        var cut = RenderHost();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("dark", Browser.AppliedThemes);
            Assert.Contains("--mud-palette-primary: rgba(255,122,92,1)", ProviderStyle(cut), StringComparison.Ordinal);
            Assert.Contains("--mud-palette-background: rgba(23,20,18,1)", ProviderStyle(cut), StringComparison.Ordinal);
        });
    }

    [Fact]
    public void StoredHighContrast_UsesTheHighContrastPalette()
    {
        Browser.Storage[BrowserThemePreferenceStore.StorageKey] = "high-contrast";

        var cut = RenderHost();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("high-contrast", Browser.AppliedThemes);
            Assert.Contains("--mud-palette-primary: rgba(158,42,18,1)", ProviderStyle(cut), StringComparison.Ordinal);
            Assert.Contains("--mud-palette-text-primary: rgba(0,0,0,1)", ProviderStyle(cut), StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task ChangingTheMode_SwitchesThePaletteAtOnce()
    {
        var cut = RenderHost();
        var state = Services.GetRequiredService<ThemeState>();

        await cut.InvokeAsync(() => state.SetModeAsync(ThemeMode.Dark));

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("--mud-palette-primary: rgba(255,122,92,1)", ProviderStyle(cut), StringComparison.Ordinal);
            Assert.Equal("dark", Browser.AppliedThemes[^1]);
        });
    }
}
