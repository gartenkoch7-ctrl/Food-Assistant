using FoodAssistant.Contracts.Shopping;

namespace FoodAssistant.UI.Shopping;

/// <summary>
/// Reads the conflict outcome of the server (ShoppingConflictResolver). On a change the incoming state
/// wins in every case but one: "checked wins" when the request was <em>unchecked</em> keeps the server's
/// checked state. On a delete the deletion only applied for no conflict or when it was already deleted.
/// </summary>
internal static class ShoppingOutcomes
{
    public static bool ChangeKeptServerState(ShoppingConflictOutcomeDto outcome, bool requestedChecked) =>
        outcome == ShoppingConflictOutcomeDto.CheckedWins && !requestedChecked;

    public static bool DeleteApplied(ShoppingConflictOutcomeDto outcome) =>
        outcome is ShoppingConflictOutcomeDto.NoConflict or ShoppingConflictOutcomeDto.BothDeleted;
}
