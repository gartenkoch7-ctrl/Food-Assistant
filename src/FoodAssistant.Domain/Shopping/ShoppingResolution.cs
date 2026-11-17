namespace FoodAssistant.Domain.Shopping;

/// <summary>Result of <see cref="ShoppingConflictResolver.Resolve"/>.</summary>
/// <param name="State">State to store and broadcast.</param>
/// <param name="Outcome">Rule that decided the result.</param>
/// <param name="ServerChanged">
/// False if the stored state stays as it was (the incoming change lost); nothing needs saving then.
/// </param>
public sealed record ShoppingResolution(ShoppingItemState State, ShoppingConflictOutcome Outcome, bool ServerChanged);
