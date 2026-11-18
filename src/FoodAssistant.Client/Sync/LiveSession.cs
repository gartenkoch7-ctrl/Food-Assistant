using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;

namespace FoodAssistant.Client.Sync;

/// <summary>One live connection for one household; replaced as a whole when the household changes.</summary>
internal sealed class LiveSession(Guid householdId)
{
    public Guid HouseholdId { get; } = householdId;

    public CancellationTokenSource Cancellation { get; } = new();

    public HubConnection? Connection { get; set; }

    /// <summary>True while the start loop runs; it handles failures itself, including the Closed event of its own stop.</summary>
    public volatile bool LoopActive;

    /// <summary>The server refused the join (not a member): final, no retry, and its Closed event is no loss.</summary>
    public volatile bool Refused;

    /// <summary>Cancels the session and releases its connection in the background: disposing may wait for the network.</summary>
    public void Retire(ILogger logger)
    {
        Cancellation.Cancel();
        _ = DisposeConnectionAsync(logger);
    }

    public async Task StopQuietlyAsync(ILogger logger)
    {
        try
        {
            await Connection!.StopAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            SyncLog.LiveConnectFailed(logger, exception);
        }
    }

    private async Task DisposeConnectionAsync(ILogger logger)
    {
        try
        {
            await Connection!.DisposeAsync().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            SyncLog.LiveConnectFailed(logger, exception);
        }
    }
}
