namespace FoodAssistant.Contracts.ShelfLife;

/// <summary>
/// Key of the shipped source (see the sources list) per value of <see cref="ShelfLifeRuleRowDto.Default"/>;
/// <see langword="null"/> where that value is not a shipped default (none, or a category override).
/// </summary>
public sealed record ShelfLifeSourceKeysDto(string? DaysAfterOpening, string? DaysFrozen, string? DaysAfterThawing, string? DaysAsLeftover);
