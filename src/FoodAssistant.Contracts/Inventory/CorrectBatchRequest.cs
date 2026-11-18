using FoodAssistant.Contracts.Common;

namespace FoodAssistant.Contracts.Inventory;

/// <summary>
/// Corrects a batch: the amount always; the package date and kind only while the batch is sealed
/// (otherwise send the current values, a change answers 422 <c>inventory.date_locked</c>).
/// </summary>
public sealed record CorrectBatchRequest(QuantityDto Quantity, DateOnly? PackageExpiresOn, ExpiryKindDto PackageExpiryKind);
