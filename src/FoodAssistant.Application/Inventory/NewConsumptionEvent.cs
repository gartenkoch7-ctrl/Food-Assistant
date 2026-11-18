using FoodAssistant.Domain.Expiry;
using FoodAssistant.Domain.Inventory;
using FoodAssistant.Domain.Quantities;

namespace FoodAssistant.Application.Inventory;

/// <summary>A use or disposal of one batch (statistics, undo).</summary>
/// <param name="Id">Event id, created by the caller so the undo payload can name it.</param>
/// <param name="ProductId">Product of the batch.</param>
/// <param name="BatchId">The batch.</param>
/// <param name="Kind">Used up, partly used or discarded.</param>
/// <param name="Reason">Reason; only for <see cref="ConsumptionKind.Discarded"/>.</param>
/// <param name="Quantity">Amount taken, in the unit of the batch.</param>
/// <param name="ExpiryStatusAtEvent">Status of the batch at that moment (waste statistics).</param>
/// <param name="UserId">Who recorded it.</param>
/// <param name="OccurredOn">Household date.</param>
public sealed record NewConsumptionEvent(
    Guid Id,
    Guid ProductId,
    Guid BatchId,
    ConsumptionKind Kind,
    ConsumptionReason? Reason,
    Quantity Quantity,
    ExpiryStatus ExpiryStatusAtEvent,
    Guid? UserId,
    DateOnly OccurredOn);
