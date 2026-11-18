namespace FoodAssistant.Contracts.Stores;

/// <summary>Stable <c>errorCode</c> values of store endpoints (ADR-0004). The UI translates these.</summary>
public static class StoreErrorCodes
{
    /// <summary>Unknown store or one of another household (404).</summary>
    public const string StoreNotFound = "stores.store_not_found";

    /// <summary>Request data is invalid; see <c>errors</c> (400).</summary>
    public const string ValidationFailed = "stores.validation_failed";
}
