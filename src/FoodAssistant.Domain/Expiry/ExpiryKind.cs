namespace FoodAssistant.Domain.Expiry;

/// <summary>Kind of an expiry date (glossary: MHD, Verbrauchsdatum).</summary>
public enum ExpiryKind
{
    /// <summary>No expiry date.</summary>
    None,

    /// <summary>"Mindesthaltbarkeitsdatum": best before, often still fine afterwards.</summary>
    BestBefore,

    /// <summary>"Verbrauchsdatum": use by, must not be eaten afterwards.</summary>
    UseBy,
}
