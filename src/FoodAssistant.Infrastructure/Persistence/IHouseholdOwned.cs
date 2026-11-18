namespace FoodAssistant.Infrastructure.Persistence;

/// <summary>
/// Marks an entity as data of exactly one household. <see cref="AppDbContext"/> gives every such
/// entity the query filter <see cref="AppDbContext.ActiveHouseholdFilter"/>, so queries only see
/// the active, membership-checked household (AGENTS.md 2.4, ADR-0002).
/// </summary>
/// <remarks>
/// Pattern for every later feature (inventory, shopping, recipes …): implement this interface,
/// nothing else. Ignoring the filter (<c>IgnoreQueryFilters([AppDbContext.ActiveHouseholdFilter])</c>)
/// is only allowed with a comment explaining why the query is safe across households.
/// </remarks>
public interface IHouseholdOwned
{
    Guid HouseholdId { get; }
}
