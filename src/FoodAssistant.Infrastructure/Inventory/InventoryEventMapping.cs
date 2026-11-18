using FoodAssistant.Application.Households;
using FoodAssistant.Application.Inventory;

namespace FoodAssistant.Infrastructure.Inventory;

/// <summary>Maps a recorded consumption to its entity.</summary>
internal static class InventoryEventMapping
{
    public static ConsumptionEvent ToEntity(NewConsumptionEvent consumption, Guid householdId, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(consumption);
        return new ConsumptionEvent
        {
            Id = consumption.Id,
            HouseholdId = householdId,
            ProductId = consumption.ProductId,
            BatchId = consumption.BatchId,
            Kind = consumption.Kind,
            Reason = consumption.Reason,
            QuantityValue = consumption.Quantity.Value,
            QuantityUnitCode = consumption.Quantity.Unit.Code,
            ExpiryStatusAtEvent = consumption.ExpiryStatusAtEvent,

            // Like the activity feed: background work is recorded as the system, not as a person.
            UserId = consumption.UserId == HouseholdScopes.SystemUserId ? null : consumption.UserId,
            OccurredAt = now,
            OccurredOn = consumption.OccurredOn,
            CreatedAt = now,
        };
    }
}
