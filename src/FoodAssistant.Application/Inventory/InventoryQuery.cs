namespace FoodAssistant.Application.Inventory;

/// <summary>Filters of the inventory overview.</summary>
/// <param name="Status"><c>expired</c>, <c>soon</c> or <c>all</c> (default); applies to batches.</param>
/// <param name="LocationId">Only batches in this location.</param>
/// <param name="Query">Part of the product name (case-insensitive).</param>
/// <param name="After">Position of the last item of the previous page.</param>
/// <param name="Take">Number of products to return.</param>
public sealed record InventoryQuery(string? Status, Guid? LocationId, string? Query, InventoryCursor? After, int Take);
