using System.Text.RegularExpressions;
using FoodAssistant.UI.Tests.Support;

namespace FoodAssistant.UI.Tests.Theme;

/// <summary>The colour table of docs/kb/design-system.md as data: token name (<c>--fa-…</c>) to value, per theme.</summary>
public static partial class DesignSystemTable
{
    private static readonly Tables Parsed = Load();

    public static IReadOnlyDictionary<string, string> Light => Parsed.Light;

    public static IReadOnlyDictionary<string, string> Dark => Parsed.Dark;

    public static IReadOnlyDictionary<string, string> HighContrast => Parsed.HighContrast;

    [GeneratedRegex("`([a-z-]+)`(?: / `(-[a-z]+)`)?")]
    private static partial Regex NameCell();

    // Rows look like "| `surface` | #fbf9f7 | #171412 | #ffffff | usage |" or, for pairs,
    // "| `status-soon` / `-bg` | #7a4b00 / #fff1d6 | … |".
    private static Tables Load()
    {
        var light = new Dictionary<string, string>();
        var dark = new Dictionary<string, string>();
        var highContrast = new Dictionary<string, string>();

        foreach (var line in File.ReadLines(RepoFiles.Path("docs/kb/design-system.md")))
        {
            if (!line.StartsWith("| `", StringComparison.Ordinal))
            {
                continue;
            }

            var cells = line.Split('|', StringSplitOptions.TrimEntries);
            var name = NameCell().Match(cells[1]);
            var suffix = name.Groups[2].Success ? name.Groups[2].Value : null;
            var key = "--fa-" + name.Groups[1].Value;

            Add(light, key, suffix, cells[2]);
            Add(dark, key, suffix, cells[3]);
            Add(highContrast, key, suffix, cells[4]);
        }

        return new Tables(light, dark, highContrast);
    }

    private static void Add(Dictionary<string, string> target, string key, string? suffix, string cell)
    {
        var values = cell.Split('/', StringSplitOptions.TrimEntries);
        target[key] = values[0];
        if (suffix is not null)
        {
            target[key + suffix] = values[1];
        }
    }

    private sealed record Tables(
        IReadOnlyDictionary<string, string> Light,
        IReadOnlyDictionary<string, string> Dark,
        IReadOnlyDictionary<string, string> HighContrast);
}
