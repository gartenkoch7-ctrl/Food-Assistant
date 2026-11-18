using FoodAssistant.Application.Activity;
using FoodAssistant.Application.Households;
using FoodAssistant.Infrastructure.Persistence;

namespace FoodAssistant.Infrastructure.Activity;

/// <summary>Adds feed entries to the context of the request; the caller's next save stores them.</summary>
internal sealed class ActivityLog(AppDbContext db, IHouseholdContext householdContext, TimeProvider time) : IActivityLog
{
    public Guid Add(ActivityEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        var householdId = householdContext.HouseholdId
            ?? throw new InvalidOperationException("Activity entries need an active household.");

        var now = time.GetUtcNow();
        var activity = new HouseholdActivity
        {
            HouseholdId = householdId,
            Area = entry.Area,
            Kind = entry.Kind,
            SubjectId = entry.SubjectId,
            ProductId = entry.ProductId,
            UserId = entry.UserId == HouseholdScopes.SystemUserId ? null : entry.UserId,
            OccurredAt = now,
            PayloadJson = entry.PayloadJson,
            SubjectVersionAfter = entry.SubjectVersionAfter,
            CreatedAt = now,
            UpdatedAt = now,
        };
        db.Add(activity);
        return activity.Id;
    }
}
