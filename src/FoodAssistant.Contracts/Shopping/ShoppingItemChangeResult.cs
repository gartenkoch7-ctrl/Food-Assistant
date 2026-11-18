namespace FoodAssistant.Contracts.Shopping;

/// <summary>Result of changing or deleting an entry: the stored state and the rule that decided it.</summary>
/// <param name="Item">The entry as stored now (after a deletion that won: its last state before removal).</param>
/// <param name="Outcome">Which conflict rule of ADR-0005 decided the result.</param>
public sealed record ShoppingItemChangeResult(ShoppingItemDto Item, ShoppingConflictOutcomeDto Outcome);
