namespace FoodAssistant.Contracts.Households;

/// <summary>
/// Result of creating an invitation. <paramref name="Token"/> is returned only here, exactly once;
/// the server keeps nothing but its hash.
/// </summary>
public sealed record CreatedInvitationResponse(Guid Id, string Token, HouseholdRole Role, DateTimeOffset ExpiresAt);
