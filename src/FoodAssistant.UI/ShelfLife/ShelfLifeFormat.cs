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

    /// <summary>Formats a localized pattern with the current culture.</summary>
    public static string Format(IStringLocalizer<ShelfLifeText> text, string key, params object?[] args)
    {
        ArgumentNullException.ThrowIfNull(text);
        return string.Format(CultureInfo.CurrentCulture, text[key].Value, args);
    }
}
