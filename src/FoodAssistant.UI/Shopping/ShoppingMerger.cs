using FoodAssistant.Client.Shopping;
using FoodAssistant.Contracts.Common;
using FoodAssistant.Contracts.Shopping;

namespace FoodAssistant.UI.Shopping;

/// <summary>
/// Merges two open items of the same product: the first gets the summed quantity (a PUT), the second
/// is deleted. Both answers are checked, because the server may resolve either against a concurrent change.
/// </summary>
internal sealed class ShoppingMerger(IShoppingClient shopping)
{
    public async Task<MergeResult> MergeAsync(Guid household, ShoppingItemDto first, ShoppingItemDto second)
    {
        QuantityDto? sum = first.Quantity is { } a && second.Quantity is { } b ? new QuantityDto(a.Value + b.Value, a.Unit) : null;
        var change = await shopping.ChangeItemAsync(
            household, first.Id,
            new ChangeShoppingItemRequest(first.Version, false, sum, first.Note ?? second.Note, first.Text, first.SectionKey),
            CancellationToken.None);
        if (!change.IsSuccess)
        {
            return new MergeResult(MergeStatus.Failed, change.Error);
        }

        // The sum was sent unchecked: only "checked wins" keeps the server's state. Every other outcome applied it,
        // so the delete must follow - stopping here would leave the summed quantity next to the duplicate.
        if (ShoppingOutcomes.ChangeKeptServerState(change.Value!.Outcome, requestedChecked: false))
        {
            return new MergeResult(MergeStatus.Conflict);
        }

        var delete = await shopping.DeleteItemAsync(household, second.Id, second.Version, CancellationToken.None);
        if (!delete.IsSuccess)
        {
            return new MergeResult(MergeStatus.HalfDone, delete.Error);
        }

        return ShoppingOutcomes.DeleteApplied(delete.Value!.Outcome)
            ? new MergeResult(MergeStatus.Merged)
            : new MergeResult(MergeStatus.HalfDone);
    }
}
