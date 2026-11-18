using FoodAssistant.UI.Common;

namespace FoodAssistant.UI.Tests.Support;

/// <summary>In-memory browser: local storage is a dictionary, the other calls are recorded.</summary>
public sealed class FakeBrowserInterop : IBrowserInterop
{
    public Dictionary<string, string> Storage { get; } = [];

    public List<string> AppliedThemes { get; } = [];

    public List<string> FocusedElements { get; } = [];

    public List<string> CultureCookies { get; } = [];

    public Task<string?> GetItemAsync(string key, CancellationToken cancellationToken = default) =>
        Task.FromResult(Storage.TryGetValue(key, out var value) ? value : null);

    public Task SetItemAsync(string key, string value, CancellationToken cancellationToken = default)
    {
        Storage[key] = value;
        return Task.CompletedTask;
    }

    public Task ApplyThemeAsync(string themeToken, CancellationToken cancellationToken = default)
    {
        AppliedThemes.Add(themeToken);
        return Task.CompletedTask;
    }

    public Task FocusElementAsync(string elementId, CancellationToken cancellationToken = default)
    {
        FocusedElements.Add(elementId);
        return Task.CompletedTask;
    }

    public Task SetCultureCookieAsync(string culture, CancellationToken cancellationToken = default)
    {
        CultureCookies.Add(culture);
        return Task.CompletedTask;
    }
}
