using Microsoft.Extensions.DependencyInjection;

namespace FoodAssistant.Application.Households;

/// <summary>
/// Scopes for background services that work on household data (reminders, recurring entries,
/// clean-up). The scope's household context is set to the given household, so the household query
/// filter applies exactly as in a request; never use <c>IgnoreQueryFilters</c> instead.
/// </summary>
/// <remarks>
/// The person of the scope is <see cref="HouseholdScopes.SystemUserId"/> with owner rights.
/// Pattern: <c>foreach (var id in await households.ListActiveHouseholdIdsAsync(ct)) { await using var scope = factory.CreateScope(id); … }</c>.
/// Registered as singleton, so hosted services can take it in their constructor.
/// </remarks>
public interface IHouseholdScopeFactory
{
    AsyncServiceScope CreateScope(Guid householdId);
}
