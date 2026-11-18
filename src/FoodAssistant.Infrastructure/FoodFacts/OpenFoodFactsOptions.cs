namespace FoodAssistant.Infrastructure.FoodFacts;

/// <summary>Open Food Facts access, section <c>FoodAssistant:OpenFoodFacts</c> (docs/kb/externe-daten.md).</summary>
public sealed class OpenFoodFactsOptions
{
    public const string SectionName = "FoodAssistant:OpenFoodFacts";

    /// <summary>Default contact in the <c>User-Agent</c>: the project page, no personal data (M1 decision F8).</summary>
    public const string DefaultContact = "https://github.com/gartenkoch7-ctrl/Food-Assistant";

    /// <summary>False turns the lookup off; the server then never contacts Open Food Facts.</summary>
    public bool Enabled { get; set; } = true;

    public string BaseUrl { get; set; } = "https://world.openfoodfacts.org/";

    /// <summary>Contact for Open Food Facts operators, sent in the <c>User-Agent</c>. Self-hosters may use their own.</summary>
    public string Contact { get; set; } = DefaultContact;

    public int TimeoutSeconds { get; set; } = 5;

    /// <summary>Own limit below the 100/min per IP that Open Food Facts allows for product reads.</summary>
    public int RequestsPerMinute { get; set; } = 60;

    /// <summary>Null when valid, otherwise what is wrong.</summary>
    public string? Validate()
    {
        if (!Uri.TryCreate(BaseUrl, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
        {
            return $"{SectionName}:BaseUrl must be an absolute https URL.";
        }

        if (string.IsNullOrWhiteSpace(Contact) || Contact.IndexOfAny(['\r', '\n', '(', ')']) >= 0)
        {
            return $"{SectionName}:Contact must be a non-empty single-line value without parentheses.";
        }

        return TimeoutSeconds <= 0 || RequestsPerMinute <= 0
            ? $"{SectionName}: TimeoutSeconds and RequestsPerMinute must be positive."
            : null;
    }
}
