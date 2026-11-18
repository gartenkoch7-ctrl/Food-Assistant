using FoodAssistant.Contracts.Inventory;
using FoodAssistant.Contracts.ShelfLife;
using FoodAssistant.Domain.Inventory;

namespace FoodAssistant.Application.ShelfLife;

/// <summary>Pure mappings between shelf-life domain types and contracts.</summary>
internal static class ShelfLifeMapping
{
    /// <summary>Storage types in display order (fridge, freezer, pantry).</summary>
    public static IReadOnlyList<StorageType> StorageTypes { get; } = [StorageType.Chilled, StorageType.Frozen, StorageType.Ambient];

    /// <summary>Parses the lower-case API name (<c>chilled</c>, <c>frozen</c>, <c>ambient</c>).</summary>
    /// <remarks>Numbers and other spellings are refused so a route has exactly one form.</remarks>
    public static bool TryParseStorageType(string? value, out StorageType storageType)
    {
        switch (value)
        {
            case "chilled":
                storageType = StorageType.Chilled;
                return true;
            case "frozen":
                storageType = StorageType.Frozen;
                return true;
            case "ambient":
                storageType = StorageType.Ambient;
                return true;
            default:
                storageType = default;
                return false;
        }
    }

    // ContractEnumTests keep both enums in step.
    public static StorageTypeDto ToDto(StorageType storageType) => (StorageTypeDto)(int)storageType;

    public static ShelfLifeDaysDto ToDto(ShelfLifeRule rule) =>
        new(rule.DaysAfterOpening, rule.DaysFrozen, rule.DaysAfterThawing, rule.DaysAsLeftover);

    /// <summary>The rule of validated days (no negative values).</summary>
    public static ShelfLifeRule ToRule(ShelfLifeDaysDto days) => new()
    {
        DaysAfterOpening = days.DaysAfterOpening,
        DaysFrozen = days.DaysFrozen,
        DaysAfterThawing = days.DaysAfterThawing,
        DaysAsLeftover = days.DaysAsLeftover,
    };

    public static bool IsEmpty(ShelfLifeDaysDto days) =>
        days.DaysAfterOpening is null && days.DaysFrozen is null && days.DaysAfterThawing is null && days.DaysAsLeftover is null;

    /// <param name="categoryKey">Category of the row.</param>
    /// <param name="storageType">Storage type of the row.</param>
    /// <param name="inherited">Values of the less specific levels, already resolved.</param>
    /// <param name="own">The household's values at this level.</param>
    /// <param name="sources">Sources of the shipped values in <paramref name="inherited"/>.</param>
    public static ShelfLifeRuleRowDto ToRow(
        string categoryKey,
        StorageType storageType,
        ShelfLifeRule inherited,
        ShelfLifeRule? own,
        ShelfLifeSourceKeysDto sources) =>
        new(
            categoryKey,
            ToDto(storageType),
            ToDto(inherited),
            own is null ? null : ToDto(own),
            ToDto(ShelfLifeRuleResolver.Resolve(own, null, inherited)),
            sources);

    /// <summary>
    /// Source per inherited value: the shipped value's source, unless a category override replaces that
    /// value (then it is the household's own value and has no source).
    /// </summary>
    public static ShelfLifeSourceKeysDto SourcesOf(DefaultShelfLifeRow? shipped, ShelfLifeRule? categoryOverride) =>
        shipped is null
            ? NoSources
            : new(
                categoryOverride?.DaysAfterOpening is null ? shipped.Sources.DaysAfterOpening : null,
                categoryOverride?.DaysFrozen is null ? shipped.Sources.DaysFrozen : null,
                categoryOverride?.DaysAfterThawing is null ? shipped.Sources.DaysAfterThawing : null,
                categoryOverride?.DaysAsLeftover is null ? shipped.Sources.DaysAsLeftover : null);

    private static ShelfLifeSourceKeysDto NoSources { get; } = new(null, null, null, null);
}
