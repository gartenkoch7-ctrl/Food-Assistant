namespace FoodAssistant.Application.Inventory;

/// <summary>An inventory action ready to be stored by <see cref="InventoryCommitter.ApplyAsync"/>.</summary>
/// <param name="Kind">Activity kind (<see cref="InventoryActivityKinds"/>).</param>
/// <param name="ProductId">Product concerned.</param>
/// <param name="Steps">Touched batches; the first one is the subject of the activity.</param>
/// <param name="StockChanged">Whether amounts changed or batches came or went (stock hooks run only then).</param>
/// <param name="VersionSent">
/// Whether the client sent the batch version (<c>If-Match</c>); only then a concurrent change answers
/// 412, otherwise 409.
/// </param>
public sealed record InventoryAction(string Kind, Guid ProductId, IReadOnlyList<BatchStep> Steps, bool StockChanged, bool VersionSent = false);
