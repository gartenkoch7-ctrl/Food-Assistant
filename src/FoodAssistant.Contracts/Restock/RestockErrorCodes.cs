namespace FoodAssistant.Contracts.Restock;

/// <summary>
/// Stable <c>errorCode</c> values of the recurring-rule endpoints (ADR-0004). The UI translates these.
/// An unknown list answers <c>shopping.list_not_found</c>, an unknown product <c>catalog.product_not_found</c> (both 404).
/// </summary>
public static class RestockErrorCodes
{
    /// <summary>Unknown rule or one of another household (404).</summary>
    public const string RuleNotFound = "restock.rule_not_found";

    /// <summary>Request data is invalid; see <c>errors</c> (400).</summary>
    public const string ValidationFailed = "restock.validation_failed";
}
