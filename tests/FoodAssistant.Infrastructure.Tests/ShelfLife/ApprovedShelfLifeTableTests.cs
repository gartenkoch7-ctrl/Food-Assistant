using System.Globalization;
using System.Text.RegularExpressions;
using FoodAssistant.Infrastructure.ShelfLife;

namespace FoodAssistant.Infrastructure.Tests.ShelfLife;

/// <summary>
/// The shipped table is exactly the one the user approved and docs/kb/haltbarkeitsregeln.md documents:
/// any change to a value or source in either place fails here until both are changed together.
/// </summary>
public sealed partial class ApprovedShelfLifeTableTests
{
    private static readonly string[] Fields = ["opening", "frozen", "thawing", "leftover"];

    [Fact]
    public void ShippedJson_MatchesTheDocumentedTable_CellByCell()
    {
        var shipped = EmbeddedDefaultShelfLifeRules.Load().Rows
            .SelectMany(r => new (int? Days, string? Source)[]
                {
                    (r.Rule.DaysAfterOpening, r.Sources.DaysAfterOpening),
                    (r.Rule.DaysFrozen, r.Sources.DaysFrozen),
                    (r.Rule.DaysAfterThawing, r.Sources.DaysAfterThawing),
                    (r.Rule.DaysAsLeftover, r.Sources.DaysAsLeftover),
                }
                .Select((v, i) => (v.Days, v.Source, Field: Fields[i]))
                .Where(v => v.Days is not null)
                .Select(v => $"{r.CategoryKey}/{r.StorageType}/{v.Field}={v.Days}@{v.Source}"))
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.Equal(DocumentedCells().Order(StringComparer.Ordinal), shipped);
    }

    [Fact]
    public void ShippedSources_AreTheDocumentedSources()
    {
        var documented = Document()
            .Select(line => SourceLine().Match(line))
            .Where(m => m.Success)
            .Select(m => m.Groups[1].Value)
            .Order(StringComparer.Ordinal);

        Assert.Equal(documented, EmbeddedDefaultShelfLifeRules.Load().Sources.Keys.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Document_StatesTheApproval()
    {
        Assert.Contains(Document(), line => line.Contains("Freigegeben vom Nutzer am 2026-11-18", StringComparison.Ordinal));
    }

    private static IEnumerable<string> DocumentedCells()
    {
        foreach (var match in Document().Select(line => TableLine().Match(line)).Where(m => m.Success))
        {
            for (var i = 0; i < Fields.Length; i++)
            {
                var cell = match.Groups[3 + i].Value.Trim();
                if (cell == "–")
                {
                    continue;
                }

                var value = Cell().Match(cell);
                Assert.True(value.Success, $"Unreadable cell '{cell}'.");
                var days = int.Parse(value.Groups[1].Value, CultureInfo.InvariantCulture);
                yield return $"{match.Groups[1].Value}/{match.Groups[2].Value}/{Fields[i]}={days}@{value.Groups[2].Value}";
            }
        }
    }

    private static string[] Document() =>
        File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "ShelfLife", "haltbarkeitsregeln.md"));

    [GeneratedRegex(@"^\| (\w+) \| (Chilled|Frozen|Ambient) \|([^|]+)\|([^|]+)\|([^|]+)\|([^|]+)\|$")]
    private static partial Regex TableLine();

    [GeneratedRegex(@"^(\d+) `(\w+)`$")]
    private static partial Regex Cell();

    [GeneratedRegex(@"^\| `(\w+)` \|")]
    private static partial Regex SourceLine();
}
