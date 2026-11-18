namespace FoodAssistant.Application.Inventory;

/// <summary>A loaded batch to delete (used up, or undo of adding it).</summary>
/// <param name="BatchId">Id of the batch.</param>
/// <param name="ExpectedVersion">Version the removal is based on; a different stored version is a conflict.</param>
public sealed record BatchRemoval(Guid BatchId, long ExpectedVersion);
