namespace FoodAssistant.Application.Households;

/// <summary>Constants of household scopes outside a request.</summary>
public static class HouseholdScopes
{
    /// <summary>
    /// Person of work done by the system (background services, <see cref="IHouseholdScopeFactory"/>).
    /// Stored as "no person" (<see langword="null"/>), never as a user id.
    /// </summary>
    public static readonly Guid SystemUserId = Guid.Empty;
}
