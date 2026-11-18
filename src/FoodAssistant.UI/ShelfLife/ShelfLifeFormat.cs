using System.Globalization;
using FoodAssistant.Contracts.Inventory;
using FoodAssistant.Contracts.ShelfLife;
using Microsoft.Extensions.Localization;

namespace FoodAssistant.UI.ShelfLife;

/// <summary>Display helpers of the shelf-life pages; pure text building over the given localizer.</summary>
public static class ShelfLifeFormat
{
    /// <summary>The four values of a row in display order with their text keys.</summary>
    public static IReadOnlyList<(string LabelKey, Func<ShelfLifeDaysDto, int?> Get)> Values { get; } =
    [
        ("ValueOpening", d => d.DaysAfterOpening),
        ("ValueFrozen", d => d.DaysFrozen),
        ("ValueThawing", d => d.DaysAfterThawing),
        ("ValueLeftover", d => d.DaysAsLeftover),
    ];

    public static string StorageName(IStringLocalizer<ShelfLifeText> text, StorageTypeDto storageType)
    {
        ArgumentNullException.ThrowIfNull(text);
        return storageType switch
        {
            StorageTypeDto.Chilled => text["StorageChilled"],
            StorageTypeDto.Frozen => text["StorageFrozen"],
            _ => text["StorageAmbient"],
        };
    }

    /// <summary>"1 Tag", "3 Tage" or "kein Wert".</summary>
    public static string Days(IStringLocalizer<ShelfLifeText> text, int? days)
    {
        ArgumentNullException.ThrowIfNull(text);
        return days switch
        {
            null => text["NoValue"],
            1 => text["DaysOne"],
            { } value => string.Format(CultureInfo.CurrentCulture, text["DaysMany"].Value, value),
        };
    }

    /// <summary>Text key of where an effective value comes from, or <see langword="null"/> without value.</summary>
    /// <param name="own">The value at this level.</param>
    /// <param name="inherited">The value of the less specific level.</param>
    /// <param name="inheritedKey"><c>OriginDefault</c> on the category page, <c>OriginCategory</c> on a product page.</param>
    public static string? OriginKey(int? own, int? inherited, string inheritedKey) =>
        own is not null ? "OriginOwn" : inherited is not null ? inheritedKey : null;

    /// <summary>
    /// Titles of the sources a row cites, in value order and each once; keys missing from
    /// <paramref name="sources"/> (e.g. not loaded yet) are skipped.
    /// </summary>
    public static IReadOnlyList<string> SourceTitles(ShelfLifeSourceKeysDto keys, IReadOnlyList<ShelfLifeSourceDto> sources)
    {
        ArgumentNullException.ThrowIfNull(keys);
        ArgumentNullException.ThrowIfNull(sources);
        return
        [
            .. new[] { keys.DaysAfterOpening, keys.DaysFrozen, keys.DaysAfterThawing, keys.DaysAsLeftover }
                .OfType<string>()
                .Distinct(StringComparer.Ordinal)
                .Select(key => sources.FirstOrDefault(s => s.Key == key)?.Title)
                .OfType<string>(),
        ];
    }

    /// <summary>"Quelle: A" or "Quellen: A; B", or <see langword="null"/> without titles.</summary>
    public static string? SourceLine(IStringLocalizer<ShelfLifeText> text, IReadOnlyList<string> titles)
    {
        ArgumentNullException.ThrowIfNull(titles);
        return titles.Count switch
        {
            0 => null,
            1 => Format(text, "SourceOfRow", titles[0]),
            _ => Format(text, "SourcesOfRow", string.Join("; ", titles)),
        };
    }

    /// <summary>Formats a localized pattern with the current culture.</summary>
    public static string Format(IStringLocalizer<ShelfLifeText> text, string key, params object?[] args)
    {
        ArgumentNullException.ThrowIfNull(text);
        return string.Format(CultureInfo.CurrentCulture, text[key].Value, args);
    }
}
