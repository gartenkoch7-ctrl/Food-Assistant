using FoodAssistant.UI.Accounts;

namespace FoodAssistant.UI.Tests.Support;

/// <summary>Clipboard that records what was written; <see cref="Works"/> = false simulates a refusing browser.</summary>
public sealed class FakeClipboard : IClipboard
{
    public bool Works { get; set; } = true;

    public List<string> Written { get; } = [];

    public Task<bool> WriteTextAsync(string text, CancellationToken cancellationToken = default)
    {
        if (Works)
        {
            Written.Add(text);
        }

        return Task.FromResult(Works);
    }
}
