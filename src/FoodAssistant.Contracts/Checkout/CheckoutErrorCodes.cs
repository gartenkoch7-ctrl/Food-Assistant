namespace FoodAssistant.Contracts.Checkout;

/// <summary>Stable <c>errorCode</c> values of the checkout endpoints (ADR-0004). The UI translates these.</summary>
public static class CheckoutErrorCodes
{
    /// <summary>Unknown list or one of another household (404).</summary>
    public const string ListNotFound = "checkout.list_not_found";

    /// <summary>Request data is invalid; see <c>errors</c> (400).</summary>
    public const string ValidationFailed = "checkout.validation_failed";

    /// <summary>The request has no lines (422).</summary>
    public const string NothingToComplete = "checkout.nothing_to_complete";
}
