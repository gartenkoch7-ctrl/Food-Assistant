using FoodAssistant.Application.Households;
using FoodAssistant.Contracts.Households;

namespace FoodAssistant.Server.Households;

/// <summary>
/// Scopes for background work in one household: the scope's <see cref="HttpHouseholdContext"/> is
/// set as if the system were an owner, so the household query filter applies as in a request.
/// </summary>
internal sealed class HouseholdScopeFactory(IServiceScopeFactory scopes) : IHouseholdScopeFactory
{
    public AsyncServiceScope CreateScope(Guid householdId)
    {
        var scope = scopes.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<HttpHouseholdContext>()
            .SetMembership(new HouseholdMembershipInfo(householdId, HouseholdScopes.SystemUserId, HouseholdRole.Owner));
        return scope;
    }
}
