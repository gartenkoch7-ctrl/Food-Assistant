using FoodAssistant.Contracts.Common;

namespace FoodAssistant.Contracts.Inventory;

/// <summary>
/// A batch ("Charge") as the clients see it. <paramref name="ExpiresOn"/> is the effective date
/// (status, FEFO); <paramref name="PackageExpiresOn"/> the printed one. <paramref name="Status"/>
/// and <paramref name="DaysUntilExpiry"/> are computed by the server for the household's today.
/// </summary>
public sealed record BatchDto(
    Guid Id,
    Guid ProductId,
    Guid StorageLocationId,
    QuantityDto Quantity,
    StorageTypeDto StorageType,
    BatchStateDto State,
    DateOnly? ExpiresOn,
    ExpiryKindDto ExpiryKind,
    DateOnly? PackageExpiresOn,
    ExpiryKindDto PackageExpiryKind,
    DateOnly? OpenedOn,
    DateOnly? FrozenOn,
    DateOnly? ThawedOn,
    DateOnly? PreparedOn,
    BatchOriginDto Origin,
    ExpiryStatusDto Status,
    int? DaysUntilExpiry,
    long Version);
