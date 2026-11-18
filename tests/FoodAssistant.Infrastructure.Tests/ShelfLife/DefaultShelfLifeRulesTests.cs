using FoodAssistant.Domain.Catalog;
using FoodAssistant.Domain.Inventory;
using FoodAssistant.Infrastructure.ShelfLife;

namespace FoodAssistant.Infrastructure.Tests.ShelfLife;

public sealed class DefaultShelfLifeRulesTests
{
    private const string Source = """
        "sources": { "bzfe": { "title": "Title", "url": "https://www.bzfe.de/", "retrieved": "2026-11-18" } }
        """;

    [Fact]
    public void ShippedTable_IsValid()
    {
        var table = EmbeddedDefaultShelfLifeRules.Load();

        Assert.All(table.Rows, row => Assert.True(table.Sources.ContainsKey(row.SourceKey)));
    }

    [Fact]
    public void ValidTable_IsParsedAndFound()
    {
        var table = EmbeddedDefaultShelfLifeRules.Parse(Table(Rule("dairy", "Chilled", opening: "3")));

        var row = Assert.Single(table.Rows);
        Assert.Equal(new ShelfLifeRule { DaysAfterOpening = 3 }, row.Rule);
        Assert.Same(row, table.Find(ProductCategories.Dairy, StorageType.Chilled));
        Assert.Null(table.Find(ProductCategories.Dairy, StorageType.Frozen));
        Assert.Equal(new Uri("https://www.bzfe.de/"), table.Sources["bzfe"].Url);
        Assert.Equal(new DateOnly(2026, 11, 18), table.Sources["bzfe"].Retrieved);
    }

    [Theory]
    [InlineData("unknown category", """{ "version": 1, SOURCE, "rules": [ RULE_UNKNOWN_CATEGORY ] }""")]
    [InlineData("duplicate row", """{ "version": 1, SOURCE, "rules": [ RULE, RULE ] }""")]
    [InlineData("negative days", """{ "version": 1, SOURCE, "rules": [ RULE_NEGATIVE ] }""")]
    [InlineData("missing source", """{ "version": 1, SOURCE, "rules": [ RULE_OTHER_SOURCE ] }""")]
    [InlineData("row without values", """{ "version": 1, SOURCE, "rules": [ RULE_EMPTY ] }""")]
    [InlineData("numeric storage type", """{ "version": 1, SOURCE, "rules": [ RULE_NUMERIC_TYPE ] }""")]
    [InlineData("unknown storage type", """{ "version": 1, SOURCE, "rules": [ RULE_UNKNOWN_TYPE ] }""")]
    [InlineData("unknown property", """{ "version": 1, SOURCE, "rules": [], "extra": 1 }""")]
    [InlineData("missing value key", """{ "version": 1, SOURCE, "rules": [ RULE_MISSING_KEY ] }""")]
    [InlineData("wrong version", """{ "version": 2, SOURCE, "rules": [] }""")]
    [InlineData("http source", """{ "version": 1, "sources": { "bzfe": { "title": "T", "url": "http://x.de/", "retrieved": "2026-11-18" } }, "rules": [] }""")]
    [InlineData("malformed json", """{ "version": 1, """)]
    public void InvalidTable_Throws(string reason, string template)
    {
        var json = template
            .Replace("SOURCE", Source, StringComparison.Ordinal)
            .Replace("RULE_UNKNOWN_CATEGORY", Rule("cake", "Chilled", opening: "3"), StringComparison.Ordinal)
            .Replace("RULE_NEGATIVE", Rule("dairy", "Chilled", opening: "-1"), StringComparison.Ordinal)
            .Replace("RULE_OTHER_SOURCE", Rule("dairy", "Chilled", opening: "3", source: "vz"), StringComparison.Ordinal)
            .Replace("RULE_EMPTY", Rule("dairy", "Chilled"), StringComparison.Ordinal)
            .Replace("RULE_NUMERIC_TYPE", Rule("dairy", "0", opening: "3", quoteType: false), StringComparison.Ordinal)
            .Replace("RULE_UNKNOWN_TYPE", Rule("dairy", "Cellar", opening: "3"), StringComparison.Ordinal)
            .Replace("RULE_MISSING_KEY", """{ "category": "dairy", "storageType": "Chilled", "daysAfterOpening": 3, "source": "bzfe" }""", StringComparison.Ordinal)
            .Replace("RULE", Rule("dairy", "Chilled", opening: "3"), StringComparison.Ordinal);

        var exception = Assert.Throws<InvalidOperationException>(() => EmbeddedDefaultShelfLifeRules.Parse(json));
        Assert.False(string.IsNullOrEmpty(exception.Message), reason);
    }

    private static string Table(params string[] rules) => $$"""{ "version": 1, {{Source}}, "rules": [ {{string.Join(",", rules)}} ] }""";

    private static string Rule(
        string category,
        string storageType,
        string opening = "null",
        string source = "bzfe",
        bool quoteType = true)
    {
        var type = quoteType ? $"\"{storageType}\"" : storageType;
        return $$"""
            { "category": "{{category}}", "storageType": {{type}}, "daysAfterOpening": {{opening}},
              "daysFrozen": null, "daysAfterThawing": null, "daysAsLeftover": null, "source": "{{source}}" }
            """;
    }
}
