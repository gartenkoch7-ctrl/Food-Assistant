namespace FoodAssistant.Contracts.Shopping;

/// <summary>Moves an entry to another list of the household.</summary>
/// <param name="ListId">Target list.</param>
public sealed record MoveShoppingItemRequest(Guid ListId);
