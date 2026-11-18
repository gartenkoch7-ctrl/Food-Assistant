namespace FoodAssistant.Application.Restock;

/// <summary>One run of the recurring rules for the active household of the current scope.</summary>
public interface IRecurringItemsRunner
{
    /// <returns>Number of entries added.</returns>
    Task<int> RunAsync(CancellationToken cancellationToken);
}
