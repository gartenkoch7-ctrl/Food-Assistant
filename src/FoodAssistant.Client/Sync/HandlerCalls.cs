using Microsoft.Extensions.Logging;

namespace FoodAssistant.Client.Sync;

internal static class HandlerCalls
{
    /// <summary>Calls the subscribers one after another; a failing page handler must neither stop the others nor the connection's receive loop.</summary>
    public static async Task RaiseAsync<T>(Delegate? subscribers, Func<T, Task> call, ILogger logger)
    {
        if (subscribers is null)
        {
            return;
        }

        foreach (var subscriber in subscribers.GetInvocationList().Cast<T>())
        {
            try
            {
                await call(subscriber).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                SyncLog.LiveHandlerFailed(logger, exception);
            }
        }
    }
}
