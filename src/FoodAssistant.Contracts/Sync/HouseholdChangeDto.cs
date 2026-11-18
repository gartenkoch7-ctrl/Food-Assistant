namespace FoodAssistant.Contracts.Sync;

/// <summary>
/// One changed entity. <paramref name="EntityType"/> is a <see cref="SyncEntityTypes"/> value;
/// <paramref name="Sequence"/> is the household's change-log position (ADR-0005).
/// </summary>
public sealed record HouseholdChangeDto(string EntityType, Guid Id, long Sequence, bool Deleted);
