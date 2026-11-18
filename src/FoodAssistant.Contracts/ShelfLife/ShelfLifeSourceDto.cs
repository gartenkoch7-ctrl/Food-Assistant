namespace FoodAssistant.Contracts.ShelfLife;

/// <summary>Public source of shipped shelf-life defaults (e.g. a consumer-advice page).</summary>
/// <param name="Key">Stable key referenced by <see cref="ShelfLifeRuleRowDto.SourceKey"/>.</param>
/// <param name="Title">Title of the source as published.</param>
/// <param name="Url">Address of the source.</param>
/// <param name="Retrieved">Day the values were taken from the source.</param>
public sealed record ShelfLifeSourceDto(string Key, string Title, string Url, DateOnly Retrieved);
