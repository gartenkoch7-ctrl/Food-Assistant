namespace FoodAssistant.Contracts.Households;

/// <summary>An invitation in the household's list (never contains the token).</summary>
public sealed record InvitationResponse(
    Guid Id,
    HouseholdRole Role,
    InvitationStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt);
