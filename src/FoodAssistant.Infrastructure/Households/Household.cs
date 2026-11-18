using FoodAssistant.Contracts.Households;

namespace FoodAssistant.Infrastructure.Households;

/// <summary>A household (tenant). Deleted households are kept 30 days and hidden by a query filter.</summary>
public sealed class Household
{
    /// <summary>Default of <see cref="ExpiryLeadDays"/>, also the column default for existing rows.</summary>
    public const int DefaultExpiryLeadDays = 2;

    public Guid Id { get; init; } = Guid.CreateVersion7();

    public required string Name { get; set; }

    /// <summary>IANA time zone id; expiry dates are interpreted in it (AGENTS.md 2.4).</summary>
    public required string TimeZoneId { get; set; }

    /// <summary>Days before the expiry date from which a batch counts as "expires soon" (M1, default 2).</summary>
    public int ExpiryLeadDays { get; set; } = DefaultExpiryLeadDays;

    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>When the household was deleted; final removal 30 days later (ADR-0002).</summary>
    public DateTimeOffset? DeletedAt { get; set; }

    /// <summary>Optimistic concurrency token, mapped to PostgreSQL <c>xmin</c>.</summary>
    public uint Version { get; set; }

    public ICollection<HouseholdMembership> Memberships { get; } = [];

    /// <summary>A new household whose first owner is <paramref name="ownerUserId"/> (never without owner).</summary>
    public static Household CreateWithOwner(string name, string timeZoneId, Guid ownerUserId, DateTimeOffset now)
    {
        var household = new Household { Name = name, TimeZoneId = timeZoneId, CreatedAt = now };
        household.Memberships.Add(new HouseholdMembership
        {
            HouseholdId = household.Id,
            UserId = ownerUserId,
            Role = HouseholdRole.Owner,
            JoinedAt = now,
        });
        return household;
    }
}
