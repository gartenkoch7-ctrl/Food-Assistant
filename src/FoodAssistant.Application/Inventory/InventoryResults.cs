using FoodAssistant.Application.Common;
using FoodAssistant.Contracts.Inventory;

namespace FoodAssistant.Application.Inventory;

/// <summary>Pure helpers of the inventory use cases: domain errors as results, outcome tag.</summary>
internal static class InventoryResults
{
    /// <summary>
    /// Runs a domain change; an invalid transition (or a date before the previous change, e.g. after
    /// the household's time zone moved) becomes 422 <paramref name="code"/>.
    /// </summary>
    public static OperationResult<InventoryActionResponse>? TryChange(Action change, string code = InventoryErrorCodes.InvalidState)
    {
        try
        {
            change();
            return null;
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentOutOfRangeException)
        {
            return OperationResult<InventoryActionResponse>.RuleViolated(code);
        }
    }

    /// <summary>Tags the trace with the outcome and returns the result.</summary>
    public static OperationResult<T> Finish<T>(System.Diagnostics.Activity? activity, OperationResult<T> result, int? batches = null)
    {
        activity?.SetTag("foodassistant.outcome", result.Status.ToString());
        if (batches is { } count)
        {
            activity?.SetTag("foodassistant.batches", count);
        }

        return result;
    }
}
