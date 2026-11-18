using FoodAssistant.Contracts.Common;

namespace FoodAssistant.Contracts.Inventory;

/// <summary>
/// Adds a batch. <paramref name="Id"/> is optional (UUIDv7 from the client, ADR-0004). In a freezer
/// a batch with package date counts as bought frozen (sealed, package date applies); without date
/// or with <paramref name="SelfFrozen"/> it is frozen at home from today (rule "frozen").
/// </summary>
public sealed record AddBatchRequest(
    Guid? Id,
    Guid ProductId,
    Guid StorageLocationId,
    QuantityDto Quantity,
    DateOnly? PackageExpiresOn,
    ExpiryKindDto PackageExpiryKind,
    BatchOriginDto Origin,
    bool SelfFrozen);
