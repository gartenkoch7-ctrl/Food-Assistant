namespace FoodAssistant.Application.ShelfLife;

/// <summary>Public source cited by default rows (title, address, day the values were taken).</summary>
public sealed record ShelfLifeSource(string Key, string Title, Uri Url, DateOnly Retrieved);
