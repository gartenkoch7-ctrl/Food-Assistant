using FoodAssistant.Contracts.Households;
using Microsoft.AspNetCore.Authorization;

namespace FoodAssistant.Server.Auth;

/// <summary>The caller needs at least <see cref="Minimum"/> in the household of the route.</summary>
public sealed class HouseholdRoleRequirement(HouseholdRole minimum) : IAuthorizationRequirement
{
    public HouseholdRole Minimum { get; } = minimum;
}
