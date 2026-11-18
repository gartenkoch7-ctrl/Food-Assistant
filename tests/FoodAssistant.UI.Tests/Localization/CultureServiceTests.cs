using System.Globalization;
using FoodAssistant.UI.Common;
using FoodAssistant.UI.Tests.Support;

namespace FoodAssistant.UI.Tests.Localization;

/// <summary>Sets process-wide default cultures, so the tests restore them afterwards.</summary>
[Collection(nameof(CultureServiceTests))]
public sealed class CultureServiceTests : IDisposable
{
    private readonly CultureInfo _originalCulture = CultureInfo.CurrentCulture;
    private readonly CultureInfo _originalUiCulture = CultureInfo.CurrentUICulture;
    private readonly CultureInfo? _originalDefaultCulture = CultureInfo.DefaultThreadCurrentCulture;
    private readonly CultureInfo? _originalDefaultUiCulture = CultureInfo.DefaultThreadCurrentUICulture;

    private readonly FakeBrowserInterop _browser = new();

    public void Dispose()
    {
        CultureInfo.CurrentCulture = _originalCulture;
        CultureInfo.CurrentUICulture = _originalUiCulture;
        CultureInfo.DefaultThreadCurrentCulture = _originalDefaultCulture;
        CultureInfo.DefaultThreadCurrentUICulture = _originalDefaultUiCulture;
    }

    [Theory]
    [InlineData("de", "de")]
    [InlineData("de-AT", "de")]
    [InlineData("DE-de", "de")]
    [InlineData("en", "en")]
    [InlineData("en-GB", "en")]
    [InlineData("fr-FR", "en")]
    [InlineData("", "en")]
    [InlineData(null, "en")]
    public void Resolve_MapsToASupportedLanguage(string? name, string expected) =>
        Assert.Equal(expected, SupportedCultures.Resolve(name));

    [Fact]
    public async Task ResolveStored_WithNothingStored_UsesTheBrowserLanguage()
    {
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("de-DE");

        var language = await new CultureService(_browser).ResolveStoredAsync(TestContext.Current.CancellationToken);

        Assert.Equal("de", language);
    }

    [Fact]
    public async Task ResolveStored_WithUnsupportedBrowserLanguage_FallsBackToEnglish()
    {
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("fr-FR");

        var language = await new CultureService(_browser).ResolveStoredAsync(TestContext.Current.CancellationToken);

        Assert.Equal("en", language);
    }

    [Fact]
    public async Task ResolveStored_PrefersTheStoredLanguage()
    {
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("de-DE");
        _browser.Storage[CultureService.StorageKey] = "en";

        var language = await new CultureService(_browser).ResolveStoredAsync(TestContext.Current.CancellationToken);

        Assert.Equal("en", language);
    }

    [Fact]
    public void Apply_SetsCurrentAndDefaultCultures()
    {
        var service = new CultureService(_browser);

        service.Apply("en-GB");

        Assert.Equal("en", CultureInfo.CurrentUICulture.Name);
        Assert.Equal("en", CultureInfo.CurrentCulture.Name);
        Assert.Equal("en", CultureInfo.DefaultThreadCurrentUICulture?.Name);
        Assert.Equal("en", service.Current);
    }

    [Fact]
    public async Task Set_AppliesStoresAndWritesTheCookie()
    {
        var service = new CultureService(_browser);

        await service.SetAsync("de-CH", TestContext.Current.CancellationToken);

        Assert.Equal("de", CultureInfo.DefaultThreadCurrentUICulture?.Name);
        Assert.Equal("de", _browser.Storage[CultureService.StorageKey]);
        Assert.Equal(["de"], _browser.CultureCookies);
    }
}
