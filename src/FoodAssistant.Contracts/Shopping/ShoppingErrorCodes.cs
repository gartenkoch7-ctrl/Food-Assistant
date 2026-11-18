namespace FoodAssistant.Contracts.Shopping;

/// <summary>
/// Stable <c>errorCode</c> values of shopping endpoints (ADR-0004). The UI translates these. An
/// unknown product answers <c>catalog.product_not_found</c> (<c>CatalogErrorCodes.ProductNotFound</c>),
/// an id already used by another list or entry <c>common.conflict</c>.
/// </summary>
public static class ShoppingErrorCodes
{
    /// <summary>Unknown list or one of another household (404).</summary>
    public const string ListNotFound = "shopping.list_not_found";

    /// <summary>Unknown entry or one of another household (404).</summary>
    public const string ItemNotFound = "shopping.item_not_found";

    /// <summary>Request data is invalid; see <c>errors</c> (400).</summary>
    public const string ValidationFailed = "shopping.validation_failed";

    /// <summary>The default list cannot be deleted or lose its default flag (409).</summary>
    public const string DefaultList = "shopping.default_list";

    /// <summary>The change is based on a version the server never handed out (409).</summary>
    public const string UnknownVersion = "shopping.unknown_version";

    /// <summary>A restricted member may only check entries off or add new ones (403).</summary>
    public const string RestrictedChange = "shopping.restricted_change";
}
