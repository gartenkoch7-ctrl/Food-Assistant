using System.Globalization;
using FoodAssistant.Contracts.Inventory;
using Microsoft.Extensions.Localization;

namespace FoodAssistant.UI.Inventory;

/// <summary>
/// Wording of a batch's expiry (docs/kb/vorrat-und-haltbarkeit.md, "Status und Formulierungen").
/// The status comes from the server; this only turns it into words.
/// </summary>
public static class ExpiryText
{
    /// <summary>From this many days on only the date is shown ("24.11." instead of "bis 24.11.").</summary>
    public const int DateOnlyFromDays = 7;

    /// <summary>"seit gestern abgelaufen", "heute", "morgen", "noch 2 Tage", "bis 24.11.", "24.11.", "kein Datum".</summary>
    public static string Describe(BatchDto batch, IStringLocalizer<InventoryText> text, CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(batch);
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(culture);
        if (batch.ExpiresOn is not { } date || batch.DaysUntilExpiry is not { } days)
        {
            return text["ExpiryNoDate"];
        }

        return batch.Status switch
        {
            ExpiryStatusDto.Expired when days == -1 => text["ExpiredYesterday"],
            ExpiryStatusDto.Expired => Format(text["ExpiredDays"], culture, -days),
            ExpiryStatusDto.Soon when days == 0 => text["ExpiresToday"],
            ExpiryStatusDto.Soon when days == 1 => text["ExpiresTomorrow"],
            ExpiryStatusDto.Soon => Format(text["ExpiresInDays"], culture, days),
            _ when days >= DateOnlyFromDays => ShortDate(date, text, culture),
            _ => Format(text["ExpiresUntil"], culture, ShortDate(date, text, culture)),
        };
    }

    /// <summary>
    /// <see cref="Describe"/> with the kind in front ("Verbrauchsdatum: morgen"): a use-by date must be
    /// recognisable without colour (KB, WCAG 1.4.1).
    /// </summary>
    public static string Label(BatchDto batch, IStringLocalizer<InventoryText> text, CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(batch);
        ArgumentNullException.ThrowIfNull(text);
        var description = Describe(batch, text, culture);
        return batch.ExpiryKind == ExpiryKindDto.None ? description : $"{text["Kind." + batch.ExpiryKind].Value}: {description}";
    }

    /// <summary>The safety hint for an expired batch: best-before "often still fine", use-by "do not eat"; otherwise null.</summary>
    public static string? Hint(BatchDto batch, IStringLocalizer<InventoryText> text)
    {
        ArgumentNullException.ThrowIfNull(batch);
        ArgumentNullException.ThrowIfNull(text);
        return (batch.Status, batch.ExpiryKind) switch
        {
            (ExpiryStatusDto.Expired, ExpiryKindDto.UseBy) => text["HintUseByExpired"].Value,
            (ExpiryStatusDto.Expired, ExpiryKindDto.BestBefore) => text["HintBestBeforeExpired"].Value,
            _ => null,
        };
    }

    private static string ShortDate(DateOnly date, IStringLocalizer<InventoryText> text, CultureInfo culture) =>
        date.ToString(text["ShortDateFormat"], culture);

    private static string Format(LocalizedString format, CultureInfo culture, object value) =>
        string.Format(culture, format.Value, value);
}
