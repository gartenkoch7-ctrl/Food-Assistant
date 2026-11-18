namespace FoodAssistant.Contracts.ShelfLife;

/// <summary>Shelf-life rules of one product, one row per storage type.</summary>
public sealed record ProductShelfLifeDto(Guid ProductId, string CategoryKey, IReadOnlyList<ShelfLifeRuleRowDto> Rows);
