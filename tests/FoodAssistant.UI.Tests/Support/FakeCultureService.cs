using System.Globalization;
using FoodAssistant.UI.Common;

namespace FoodAssistant.UI.Tests.Support;

/// <summary>Reads the test's culture and records language changes, without touching process-wide defaults.</summary>
public sealed class FakeCultureService : ICultureService
{
    public List<string> Chosen { get; } = [];

    public string Current => SupportedCultures.Resolve(CultureInfo.CurrentUICulture.Name);

    public Task<string> ResolveStoredAsync(CancellationToken cancellationToken = default) => Task.FromResult(Current);

    public void Apply(string culture) => Chosen.Add(culture);

    public Task SetAsync(string culture, CancellationToken cancellationToken = default)
    {
        Chosen.Add(culture);
        return Task.CompletedTask;
    }
}
