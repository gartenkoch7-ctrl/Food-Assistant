using FoodAssistant.Application.ShelfLife;
using FoodAssistant.Contracts.ShelfLife;
using FoodAssistant.Domain.Catalog;
using FoodAssistant.Domain.Inventory;

namespace FoodAssistant.Infrastructure.ShelfLife;

/// <summary>Checks one rule of the default table (format version 2) and turns it into a row.</summary>
internal static class DefaultShelfLifeRowParser
{
    /// <exception cref="InvalidOperationException">The rule is inconsistent.</exception>
    public static DefaultShelfLifeRow ToRow(RuleJson rule, IReadOnlyDictionary<string, ShelfLifeSource> sources)
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

        ValueJson?[] values = [rule.DaysAfterOpening, rule.DaysFrozen, rule.DaysAfterThawing, rule.DaysAsLeftover];
        if (values.All(v => v is null))
        {
            throw new InvalidOperationException($"Shelf-life rule {name}: no value.");
        }

        foreach (var value in values.OfType<ValueJson>())
        {
            Check(value, name, sources);
        }

        var parsed = new ShelfLifeRule
        {
            DaysAfterOpening = rule.DaysAfterOpening?.Days,
            DaysFrozen = rule.DaysFrozen?.Days,
            DaysAfterThawing = rule.DaysAfterThawing?.Days,
            DaysAsLeftover = rule.DaysAsLeftover?.Days,
        };
        var cited = new ShelfLifeRuleSources(
            rule.DaysAfterOpening?.Source,
            rule.DaysFrozen?.Source,
            rule.DaysAfterThawing?.Source,
            rule.DaysAsLeftover?.Source);
        return new DefaultShelfLifeRow(rule.Category, rule.StorageType, parsed, cited);
    }

    private static void Check(ValueJson value, string name, IReadOnlyDictionary<string, ShelfLifeSource> sources)
    {
        // Same range as user input: negative is impossible, more than ten years is a typing error.
        if (value.Days is < 0 or > ShelfLifeLimits.MaxDays)
        {
            throw new InvalidOperationException($"Shelf-life rule {name}: {value.Days} days is outside 0–{ShelfLifeLimits.MaxDays}.");
        }

        if (!sources.ContainsKey(value.Source))
        {
            throw new InvalidOperationException($"Shelf-life rule {name}: unknown source '{value.Source}'.");
        }
    }

    /// <summary>One row as stored: all four value keys are required, <c>null</c> means "no value".</summary>
    internal sealed record RuleJson(
        string Category,
        StorageType StorageType,
        ValueJson? DaysAfterOpening,
        ValueJson? DaysFrozen,
        ValueJson? DaysAfterThawing,
        ValueJson? DaysAsLeftover);

    /// <summary>A value with the key of the source it is taken from.</summary>
    internal sealed record ValueJson(int Days, string Source);
}
