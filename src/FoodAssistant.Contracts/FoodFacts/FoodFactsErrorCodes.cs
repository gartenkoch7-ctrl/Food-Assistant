namespace FoodAssistant.Contracts.FoodFacts;

/// <summary>Stable <c>errorCode</c> values of the barcode lookup (ADR-0004). The UI translates these.</summary>
public static class FoodFactsErrorCodes
{
    /// <summary>Open Food Facts does not know the barcode (404).</summary>
    public const string NotFound = "food_facts.not_found";

    /// <summary>Open Food Facts is disabled, throttled or unreachable and nothing is cached (503).</summary>
    public const string Unavailable = "food_facts.unavailable";

    /// <summary>The barcode is not 8, 12, 13 or 14 digits (400).</summary>
    public const string InvalidBarcode = "food_facts.invalid_barcode";
}
