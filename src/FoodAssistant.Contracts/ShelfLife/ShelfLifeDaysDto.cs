namespace FoodAssistant.Contracts.ShelfLife;

/// <summary>
/// Shelf-life values in days for one category or product and one storage type. <see langword="null"/>
/// means "no value at this level" (inherited, or no computed date at all).
/// </summary>
public sealed record ShelfLifeDaysDto(int? DaysAfterOpening, int? DaysFrozen, int? DaysAfterThawing, int? DaysAsLeftover);
