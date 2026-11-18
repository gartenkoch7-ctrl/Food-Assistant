namespace FoodAssistant.Contracts.Catalog;

/// <summary>A product in search results and lists.</summary>
public sealed record ProductSummaryDto(Guid Id, string Name, string? Brand, string CategoryKey, string DefaultUnit);
