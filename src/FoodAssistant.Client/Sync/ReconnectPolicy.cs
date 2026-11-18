using Microsoft.AspNetCore.SignalR.Client;

namespace FoodAssistant.Client.Sync;

/// <summary>Quick retries first, then a steady pause; they never end: a phone may be offline for hours and should come back by itself.</summary>
internal sealed class ReconnectPolicy(TimeSpan longInterval) : IRetryPolicy
{
    public static readonly TimeSpan DefaultLongInterval = TimeSpan.FromSeconds(30);

    private static readonly TimeSpan[] Delays =
        [TimeSpan.Zero, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10)];

    public TimeSpan? NextRetryDelay(RetryContext retryContext) =>
        retryContext.PreviousRetryCount < Delays.Length ? Delays[retryContext.PreviousRetryCount] : longInterval;
}
