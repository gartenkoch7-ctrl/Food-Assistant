using FoodAssistant.Application.ShelfLife;
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
    public void ShippedTable_IsTheApprovedTable()
    {
        // Approved by the user on 2026-11-18 (docs/kb/haltbarkeitsregeln.md); a change here needs a new approval.
        var table = EmbeddedDefaultShelfLifeRules.Load();

        Assert.Equal(29, table.Rows.Count);
        Assert.Equal(40, table.Rows.Sum(r => CountValues(r.Rule)));
        Assert.Equal(9, table.Sources.Count);
        Assert.All(table.Rows, row => Assert.All(row.Sources.Keys, key => Assert.True(table.Sources.ContainsKey(key))));
        Assert.All(table.Sources.Values, s => Assert.Equal(Uri.UriSchemeHttps, s.Url.Scheme));
    }

    [Fact]
    public void ShippedTable_CitesASourceForEveryValue()
    {
        var table = EmbeddedDefaultShelfLifeRules.Load();

        Assert.All(table.Rows, row =>
        {
            Assert.Equal(row.Rule.DaysAfterOpening is null, row.Sources.DaysAfterOpening is null);
            Assert.Equal(row.Rule.DaysFrozen is null, row.Sources.DaysFrozen is null);
            Assert.Equal(row.Rule.DaysAfterThawing is null, row.Sources.DaysAfterThawing is null);
            Assert.Equal(row.Rule.DaysAsLeftover is null, row.Sources.DaysAsLeftover is null);
        });
    }

    [Fact]
    public void ShippedTable_HoldsTheApprovedSafetyValues()
    {
        var table = EmbeddedDefaultShelfLifeRules.Load();

        var meat = table.Find(ProductCategories.Meat, StorageType.Chilled)!;
        Assert.Equal(new ShelfLifeRule { DaysAfterOpening = 1, DaysAfterThawing = 0, DaysAsLeftover = 2 }, meat.Rule);
        Assert.Equal(new ShelfLifeRuleSources("bzfe_schwein", null, "bfr_privathaushalt", "bzfe_hygiene"), meat.Sources);
        Assert.Equal(new ShelfLifeRule { DaysAfterOpening = 3 }, table.Find(ProductCategories.Dairy, StorageType.Chilled)!.Rule);

        // Leftovers outside the fridge never last beyond the day (BZfE: use unrefrigerated food the same day).
        Assert.All(table.Rows.Where(r => r.StorageType == StorageType.Ambient), r => Assert.Equal(0, r.Rule.DaysAsLeftover));

        // No refreezing after thawing: frozen rows carry no "after thawing" value.
        Assert.All(table.Rows.Where(r => r.StorageType == StorageType.Frozen), r => Assert.Null(r.Rule.DaysAfterThawing));
        Assert.Null(table.Find(ProductCategories.Spices, StorageType.Ambient));
    }

    [Fact]
    public void ValidTable_IsParsedAndFound()
    {
        var table = EmbeddedDefaultShelfLifeRules.Parse(Table(Rule("dairy", "Chilled", opening: Value(3))));

        var row = Assert.Single(table.Rows);
        Assert.Equal(new ShelfLifeRule { DaysAfterOpening = 3 }, row.Rule);
        Assert.Equal(new ShelfLifeRuleSources("bzfe", null, null, null), row.Sources);
        Assert.Same(row, table.Find(ProductCategories.Dairy, StorageType.Chilled));
        Assert.Null(table.Find(ProductCategories.Dairy, StorageType.Frozen));
        Assert.Equal(new Uri("https://www.bzfe.de/"), table.Sources["bzfe"].Url);
        Assert.Equal(new DateOnly(2026, 11, 18), table.Sources["bzfe"].Retrieved);
    }

    // Each broken table is otherwise valid (every source cited), so the check named by the fragment is what fails.
    [Theory]
    [InlineData("unknown category", "RULES", "cake|Chilled|3|bzfe")]
    [InlineData("Duplicate", "RULES", "dairy|Chilled|3|bzfe", "dairy|Chilled|2|bzfe")]
    [InlineData("outside 0", "RULES", "dairy|Chilled|-1|bzfe")]
    [InlineData("outside 0", "RULES", "dairy|Chilled|3651|bzfe")]
    [InlineData("unknown source", "RULES", "dairy|Chilled|3|vz", "cheese|Frozen|60|bzfe")]
    [InlineData("no value", "RULES", "dairy|Chilled||", "cheese|Frozen|60|bzfe")]
    [InlineData("not valid JSON", "RULES", "dairy|Cellar|3|bzfe")]
    [InlineData("not cited", "RULES")]
    [InlineData("Unsupported", """{ "version": 1, SOURCE, "rules": [ VALID ] }""")]
    [InlineData("not valid JSON", """{ "version": 2, SOURCE, "rules": [ VALID ], "extra": 1 }""")]
    [InlineData("not valid JSON", """{ "version": 2, SOURCE, "rules": [ VALID, { "category": "dairy", "storageType": 0, "daysAfterOpening": { "days": 3, "source": "bzfe" }, "daysFrozen": null, "daysAfterThawing": null, "daysAsLeftover": null } ] }""")]
    [InlineData("not valid JSON", """{ "version": 2, SOURCE, "rules": [ VALID, { "category": "dairy", "storageType": "Chilled", "daysAfterOpening": { "days": 3, "source": "bzfe" } } ] }""")]
    [InlineData("not valid JSON", """{ "version": 2, SOURCE, "rules": [ VALID, { "category": "dairy", "storageType": "Chilled", "daysAfterOpening": { "days": 3 }, "daysFrozen": null, "daysAfterThawing": null, "daysAsLeftover": null } ] }""")]
    [InlineData("not valid JSON", """{ "version": 2, SOURCE, "rules": [ VALID, { "category": "dairy", "storageType": "Chilled", "daysAfterOpening": 3, "daysFrozen": null, "daysAfterThawing": null, "daysAsLeftover": null } ] }""")]
    [InlineData("https", """{ "version": 2, "sources": { "bzfe": { "title": "T", "url": "http://x.de/", "retrieved": "2026-11-18" } }, "rules": [ VALID ] }""")]
    [InlineData("not valid JSON", """{ "version": 2, """)]
    public void InvalidTable_Throws(string expectedFragment, string template, params string[] rules)
    {
        var json = template == "RULES"
            ? Table([.. rules.Select(FromShortForm)])
            : template
                .Replace("SOURCE", Source, StringComparison.Ordinal)
                .Replace("VALID", FromShortForm("cheese|Frozen|60|bzfe"), StringComparison.Ordinal);

        var exception = Assert.Throws<InvalidOperationException>(() => EmbeddedDefaultShelfLifeRules.Parse(json));
        Assert.Contains(expectedFragment, exception.Message, StringComparison.Ordinal);
    }

    private static int CountValues(ShelfLifeRule rule) =>
        new[] { rule.DaysAfterOpening, rule.DaysFrozen, rule.DaysAfterThawing, rule.DaysAsLeftover }.Count(d => d is not null);

    private static string Table(params string[] rules) => $$"""{ "version": 2, {{Source}}, "rules": [ {{string.Join(",", rules)}} ] }""";

    // "category|storageType|openingDays|openingSource"; empty days = no value.
    private static string FromShortForm(string rule)
    {
        var parts = rule.Split('|');
        return Rule(parts[0], parts[1], parts[2].Length == 0 ? "null" : Value(int.Parse(parts[2], System.Globalization.CultureInfo.InvariantCulture), parts[3]));
    }

    private static string Value(int days, string source = "bzfe") => $$"""{ "days": {{days}}, "source": "{{source}}" }""";

    private static string Rule(string category, string storageType, string opening = "null") => $$"""
        { "category": "{{category}}", "storageType": "{{storageType}}", "daysAfterOpening": {{opening}},
          "daysFrozen": null, "daysAfterThawing": null, "daysAsLeftover": null }
        """;
}
