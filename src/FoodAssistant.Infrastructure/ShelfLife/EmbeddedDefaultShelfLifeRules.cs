using System.Text.Json;
using System.Text.Json.Serialization;
using FoodAssistant.Application.ShelfLife;
using FoodAssistant.Contracts.ShelfLife;
using FoodAssistant.Domain.Catalog;
using FoodAssistant.Domain.Inventory;

namespace FoodAssistant.Infrastructure.ShelfLife;

/// <summary>
/// The default shelf-life table shipped as embedded <c>default-shelf-life-rules.json</c> (format in
/// docs/plan/features/m01-b1-haltbarkeitsregeln.md). Loaded once; every inconsistency fails loudly
/// because a wrong default would silently produce wrong use-by dates.
/// </summary>
internal sealed class EmbeddedDefaultShelfLifeRules : IDefaultShelfLifeRules
{
    internal const string ResourceName = "FoodAssistant.Infrastructure.ShelfLife.default-shelf-life-rules.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        RespectNullableAnnotations = true,
        RespectRequiredConstructorParameters = true,
        AllowDuplicateProperties = false,
        Converters = { new JsonStringEnumConverter<StorageType>(allowIntegerValues: false) },
    };

    private readonly Dictionary<(string Category, StorageType Type), DefaultShelfLifeRow> _byKey;

    private EmbeddedDefaultShelfLifeRules(
        IReadOnlyList<DefaultShelfLifeRow> rows,
        IReadOnlyDictionary<string, ShelfLifeSource> sources)
    {
        Rows = rows;
        Sources = sources;
        _byKey = rows.ToDictionary(r => (r.CategoryKey, r.StorageType));
    }

    public IReadOnlyList<DefaultShelfLifeRow> Rows { get; }

    public IReadOnlyDictionary<string, ShelfLifeSource> Sources { get; }

    public DefaultShelfLifeRow? Find(string categoryKey, StorageType storageType) =>
        _byKey.GetValueOrDefault((categoryKey, storageType));

    /// <summary>Reads the embedded table.</summary>
    /// <exception cref="InvalidOperationException">The resource is missing or invalid.</exception>
    public static EmbeddedDefaultShelfLifeRules Load()
    {
        using var stream = typeof(EmbeddedDefaultShelfLifeRules).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Embedded resource {ResourceName} is missing.");
        using var reader = new StreamReader(stream);
        return Parse(reader.ReadToEnd());
    }

    /// <summary>Parses and checks a table (version 1).</summary>
    /// <exception cref="InvalidOperationException">The JSON is malformed or the table inconsistent.</exception>
    internal static EmbeddedDefaultShelfLifeRules Parse(string json)
    {
        TableJson table;
        try
        {
            table = JsonSerializer.Deserialize<TableJson>(json, JsonOptions)
                ?? throw new InvalidOperationException("The shelf-life table is empty.");
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException($"The shelf-life table is not valid JSON: {exception.Message}", exception);
        }

        if (table.Version != 1)
        {
            throw new InvalidOperationException($"Unsupported shelf-life table version {table.Version}.");
        }

        var sources = table.Sources.ToDictionary(s => s.Key, s => ToSource(s.Key, s.Value), StringComparer.Ordinal);
        var rows = new List<DefaultShelfLifeRow>(table.Rules.Count);
        var seen = new HashSet<(string, StorageType)>();
        foreach (var rule in table.Rules)
        {
            var row = ToRow(rule, sources);
            if (!seen.Add((row.CategoryKey, row.StorageType)))
            {
                throw new InvalidOperationException($"Duplicate shelf-life rule {row.CategoryKey}/{row.StorageType}.");
            }

            rows.Add(row);
        }

        return new EmbeddedDefaultShelfLifeRules(rows, sources);
    }

    private static ShelfLifeSource ToSource(string key, SourceJson source)
    {
        if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(source.Title))
        {
            throw new InvalidOperationException("A shelf-life source needs a key and a title.");
        }

        if (!Uri.TryCreate(source.Url, UriKind.Absolute, out var url) || url.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidOperationException($"Shelf-life source {key} needs an absolute https URL.");
        }

        return new ShelfLifeSource(key, source.Title, url, source.Retrieved);
    }

    private static DefaultShelfLifeRow ToRow(RuleJson rule, Dictionary<string, ShelfLifeSource> sources)
    {
        var name = $"{rule.Category}/{rule.StorageType}";
        if (!ProductCategories.IsKnown(rule.Category))
        {
            throw new InvalidOperationException($"Shelf-life rule {name}: unknown category.");
        }

        if (!Enum.IsDefined(rule.StorageType))
        {
            throw new InvalidOperationException($"Shelf-life rule {name}: unknown storage type.");
        }

        if (!sources.ContainsKey(rule.Source))
        {
            throw new InvalidOperationException($"Shelf-life rule {name}: unknown source '{rule.Source}'.");
        }

        if (rule.DaysAfterOpening is null && rule.DaysFrozen is null && rule.DaysAfterThawing is null && rule.DaysAsLeftover is null)
        {
            throw new InvalidOperationException($"Shelf-life rule {name}: no value.");
        }

        int?[] days = [rule.DaysAfterOpening, rule.DaysFrozen, rule.DaysAfterThawing, rule.DaysAsLeftover];
        if (days.Any(d => d > ShelfLifeLimits.MaxDays))
        {
            // Same range as user input: more than ten years is a typing error in the table.
            throw new InvalidOperationException($"Shelf-life rule {name}: more than {ShelfLifeLimits.MaxDays} days.");
        }

        try
        {
            var values = new ShelfLifeRule
            {
                DaysAfterOpening = rule.DaysAfterOpening,
                DaysFrozen = rule.DaysFrozen,
                DaysAfterThawing = rule.DaysAfterThawing,
                DaysAsLeftover = rule.DaysAsLeftover,
            };
            return new DefaultShelfLifeRow(rule.Category, rule.StorageType, values, rule.Source);
        }
        catch (ArgumentOutOfRangeException exception)
        {
            throw new InvalidOperationException($"Shelf-life rule {name}: negative days.", exception);
        }
    }

    private sealed record TableJson(int Version, Dictionary<string, SourceJson> Sources, List<RuleJson> Rules);

    private sealed record SourceJson(string Title, string Url, DateOnly Retrieved);

    private sealed record RuleJson(
        string Category,
        StorageType StorageType,
        int? DaysAfterOpening,
        int? DaysFrozen,
        int? DaysAfterThawing,
        int? DaysAsLeftover,
        string Source);
}
