namespace FoodAssistant.Application.FoodFacts;

/// <summary>Outcome of a barcode lookup.</summary>
public enum FoodFactsStatus
{
    Found,
    NotFound,

    /// <summary>Disabled, throttled, timed out or failed; nothing usable cached.</summary>
    Unavailable,
}
