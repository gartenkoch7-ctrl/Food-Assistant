namespace FoodAssistant.UI.Common;

/// <summary>
/// Announces short status messages to screen readers without moving focus (WCAG 4.1.3). Live changes
/// from other household members use it, so they are noticed but do not interrupt.
/// </summary>
public interface ILiveAnnouncer
{
    /// <summary>The message currently in the live region.</summary>
    string Message { get; }

    /// <summary>Raised after <see cref="Announce"/>; <see cref="LiveRegion"/> re-renders.</summary>
    event Action? Changed;

    void Announce(string message);
}
