using FoodAssistant.Contracts.Catalog;
using FoodAssistant.Contracts.Common;

namespace FoodAssistant.Contracts.Inventory;

/// <summary>
/// A product with its batches in the inventory overview. <paramref name="Totals"/> holds one sum
/// per group of convertible units (e.g. "1.5 l" and "2 package").
/// </summary>
public sealed record InventoryProductDto(
    ProductSummaryDto Product,
    IReadOnlyList<QuantityDto> Totals,
    ExpiryStatusDto WorstStatus,
    DateOnly? NextExpiry,
    IReadOnlyList<BatchDto> Batches);
