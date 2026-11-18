namespace FoodAssistant.Contracts.ShelfLife;

/// <summary>
/// Stable <c>errorCode</c> values of shelf-life endpoints (ADR-0004). The UI translates these. An
/// unknown product reuses <c>catalog.product_not_found</c>.
/// </summary>
public static class ShelfLifeErrorCodes
{
    /// <summary>Request data is invalid; see <c>errors</c> (400).</summary>
    public const string ValidationFailed = "shelf_life.validation_failed";

    /// <summary>The category key is not one of the fixed categories (404).</summary>
    public const string UnknownCategory = "shelf_life.unknown_category";

    /// <summary>The storage type is not <c>chilled</c>, <c>frozen</c> or <c>ambient</c> (404).</summary>
    public const string UnknownStorageType = "shelf_life.unknown_storage_type";
}
