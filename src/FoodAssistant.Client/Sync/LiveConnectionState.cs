namespace FoodAssistant.Client.Sync;

/// <summary>State of the live-update connection to the household hub (ADR-0005).</summary>
public enum LiveConnectionState
{
    Disconnected,
    Connecting,
    Connected,
    Reconnecting,
}
