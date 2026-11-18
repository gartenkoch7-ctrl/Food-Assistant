namespace FoodAssistant.UI.Common;

/// <summary>
/// UI languages (ADR-0008). New languages need a <c>.resx</c> per text group and an entry here;
/// anything unsupported falls back to English.
/// </summary>
public static class SupportedCultures
{
    public const string German = "de";

    public const string English = "en";

    public const string Fallback = English;

    public static IReadOnlyList<string> All { get; } = [German, English];

    /// <summary>Maps any culture name (<c>de-AT</c>, <c>en-GB</c>, <c>fr</c>, null) to a supported language.</summary>
    public static string Resolve(string? cultureName)
    {
        if (string.IsNullOrWhiteSpace(cultureName))
        {
            return Fallback;
        }

        var language = cultureName.Split('-', '_')[0].Trim();
        foreach (var supported in All)
        {
            if (string.Equals(supported, language, StringComparison.OrdinalIgnoreCase))
            {
                return supported;
            }
        }

        return Fallback;
    }
}
