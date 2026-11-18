namespace FoodAssistant.Contracts.Catalog;

/// <summary>Stable <c>errorCode</c> values of catalog endpoints (ADR-0004). The UI translates these.</summary>
public static class CatalogErrorCodes
{
    /// <summary>Unknown product or one of another household (404).</summary>
    public const string ProductNotFound = "catalog.product_not_found";

    /// <summary>Unknown storage location or one of another household (404).</summary>
    public const string LocationNotFound = "catalog.location_not_found";

    /// <summary>Request data is invalid; see <c>errors</c> (400).</summary>
    public const string ValidationFailed = "catalog.validation_failed";

    /// <summary>A barcode already belongs to another product of the household (409).</summary>
    public const string BarcodeInUse = "catalog.barcode_in_use";

    /// <summary>A product with this name already exists in the household (409).</summary>
    public const string ProductExists = "catalog.product_exists";

    /// <summary>The product still has stock and cannot be archived (409).</summary>
    public const string ProductHasStock = "catalog.product_has_stock";

    /// <summary>The storage location still holds batches and cannot be deleted (409).</summary>
    public const string LocationInUse = "catalog.location_in_use";

    /// <summary>The storage type cannot change while the location holds batches (409).</summary>
    public const string LocationTypeLocked = "catalog.location_type_locked";
}
