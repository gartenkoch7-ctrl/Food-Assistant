using FoodAssistant.UI.Common;
using FoodAssistant.UI.Tests.Support;
using FoodAssistant.UI.Theme;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;

namespace FoodAssistant.UI.Tests.Theme;

public sealed class BrowserThemePreferenceStoreTests
{
    /// <summary>A JS runtime that fails like the browser does when storage is blocked or the page is prerendering.</summary>
    private sealed class FailingJsRuntime(Exception failure) : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => throw failure;

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) =>
            throw failure;
    }

    public static TheoryData<Exception> Failures => new()
    {
        new InvalidOperationException("JavaScript interop calls cannot be issued at this time."),
        new JSException("QuotaExceededError"),
        new JSDisconnectedException("circuit gone"),
        new TaskCanceledException(),
    };

    private static BrowserInterop BrokenBrowser(Exception failure) =>
        new(new FailingJsRuntime(failure), NullLogger<BrowserInterop>.Instance);

    [Fact]
    public async Task SaveThenLoad_RoundTrips()
    {
        var browser = new FakeBrowserInterop();
        var store = new BrowserThemePreferenceStore(browser);

        await store.SaveAsync(ThemeMode.HighContrast, TestContext.Current.CancellationToken);

        Assert.Equal("high-contrast", browser.Storage[BrowserThemePreferenceStore.StorageKey]);
        Assert.Equal(ThemeMode.HighContrast, await store.LoadAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Load_WithNothingStored_ReturnsSystem()
    {
        var store = new BrowserThemePreferenceStore(new FakeBrowserInterop());

        Assert.Equal(ThemeMode.System, await store.LoadAsync(TestContext.Current.CancellationToken));
    }

    [Theory]
    [MemberData(nameof(Failures))]
    public async Task Load_WhenStorageFails_ReturnsSystem(Exception failure)
    {
        var store = new BrowserThemePreferenceStore(BrokenBrowser(failure));

        Assert.Equal(ThemeMode.System, await store.LoadAsync(TestContext.Current.CancellationToken));
    }

    [Theory]
    [MemberData(nameof(Failures))]
    public async Task Save_WhenStorageFails_DoesNotThrow(Exception failure)
    {
        var store = new BrowserThemePreferenceStore(BrokenBrowser(failure));

        await store.SaveAsync(ThemeMode.Dark, TestContext.Current.CancellationToken);
    }

    [Theory]
    [MemberData(nameof(Failures))]
    public async Task OtherBrowserCalls_WhenUnavailable_DoNotThrow(Exception failure)
    {
        var browser = BrokenBrowser(failure);
        var token = TestContext.Current.CancellationToken;

        await browser.ApplyThemeAsync("dark", token);
        await browser.FocusElementAsync("main-content", token);
        await browser.SetCultureCookieAsync("de", token);
        Assert.Null(await browser.GetItemAsync("any", token));
    }

    [Fact]
    public async Task UnexpectedFailures_AreNotSwallowed()
    {
        var browser = BrokenBrowser(new NotSupportedException("a bug, not a missing browser"));

        await Assert.ThrowsAsync<NotSupportedException>(() => browser.ApplyThemeAsync("dark", TestContext.Current.CancellationToken));
    }
}
