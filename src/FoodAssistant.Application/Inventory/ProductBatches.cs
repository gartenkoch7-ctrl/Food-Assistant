using FoodAssistant.Contracts.Catalog;

namespace FoodAssistant.Application.Inventory;

/// <summary>A product with its stored batches.</summary>
public sealed record ProductBatches(ProductSummaryDto Product, IReadOnlyList<BatchContext> Batches);
