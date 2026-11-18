using FoodAssistant.Contracts.Households;

namespace FoodAssistant.Application.Households;

/// <summary>Data to store for a new invitation; only the hash of the token is kept.</summary>
public sealed record NewInvitation(
    Guid HouseholdId,
    HouseholdRole Role,
    string TokenHash,
    Guid CreatedByUserId,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt);
