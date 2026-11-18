namespace FoodAssistant.Application.Households;

/// <summary>A freshly generated invitation token and its hash. Only <see cref="Hash"/> is stored.</summary>
public sealed record InvitationToken(string Token, string Hash);
