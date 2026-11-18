namespace FoodAssistant.Application.ShelfLife;

/// <summary>
/// Source key per value of a default row (format version 2): each value cites the public source it
/// was taken from; <see langword="null"/> exactly where the row has no value.
/// </summary>
public sealed record ShelfLifeRuleSources(
    string? DaysAfterOpening,
    string? DaysFrozen,
    string? DaysAfterThawing,
    string? DaysAsLeftover)
{
    /// <summary>The cited keys in value order, each once.</summary>
    public IReadOnlyList<string> Keys =>
        [.. new[] { DaysAfterOpening, DaysFrozen, DaysAfterThawing, DaysAsLeftover }.OfType<string>().Distinct(StringComparer.Ordinal)];
}
