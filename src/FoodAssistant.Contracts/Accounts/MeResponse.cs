namespace FoodAssistant.Contracts.Accounts;

/// <summary>Answer of <c>GET /api/v1/me</c>: the signed-in person and their households.</summary>
public sealed record MeResponse(
    Guid UserId,
    string UserName,
    string DisplayName,
    bool IsServerAdmin,
    bool MustChangePassword,
    IReadOnlyList<MeHouseholdResponse> Households);
