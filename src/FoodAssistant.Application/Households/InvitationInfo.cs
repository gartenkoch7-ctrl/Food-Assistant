using FoodAssistant.Contracts.Households;

namespace FoodAssistant.Application.Households;

/// <summary>Stored state of an invitation (the token itself is never stored).</summary>
public sealed record InvitationInfo(
    Guid Id,
    Guid HouseholdId,
    HouseholdRole Role,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt,
    DateTimeOffset? AcceptedAt,
    DateTimeOffset? RevokedAt);
