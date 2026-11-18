using FoodAssistant.Application.Common;
using FoodAssistant.Contracts.Common;

namespace FoodAssistant.Application.Shopping;

/// <summary>
/// Runs a read-decide-write attempt again once when another request changed the entry between
/// reading and saving. Shopping entries are not refused on a stale version (ADR-0005): the second
/// attempt reads the fresh state and decides again by the conflict rules.
/// </summary>
internal static class ConcurrencyRetry
{
    /// <param name="attempt">One attempt; <see langword="null"/> means the save lost a race and nothing was stored.</param>
    public static async Task<OperationResult<T>> RunAsync<T>(Func<Task<OperationResult<T>?>> attempt)
    {
        ArgumentNullException.ThrowIfNull(attempt);
        for (var i = 0; i < 2; i++)
        {
            if (await attempt().ConfigureAwait(false) is { } result)
            {
                return result;
            }
        }

        // Two lost races in a row: the entry is being changed continuously; the client retries later.
        return OperationResult<T>.Conflict(ErrorCodes.Conflict);
    }
}
