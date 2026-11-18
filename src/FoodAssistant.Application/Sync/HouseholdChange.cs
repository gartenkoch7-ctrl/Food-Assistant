namespace FoodAssistant.Application.Sync;

/// <summary>
/// One committed change of a synchronised entity (ADR-0005). <paramref name="EntityType"/> is a
/// <c>SyncEntityTypes</c> value, <paramref name="Sequence"/> its position in the household's change log.
/// </summary>
public sealed record HouseholdChange(string EntityType, Guid EntityId, long Sequence, bool Deleted);
