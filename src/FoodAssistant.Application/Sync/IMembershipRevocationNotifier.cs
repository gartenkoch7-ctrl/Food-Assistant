namespace FoodAssistant.Application.Sync;

/// <summary>
/// Tells the live-update layer that a person lost access to a household, so open connections stop
/// receiving its changes at once instead of at the next reconnect (ADR-0005).
/// </summary>
public interface IMembershipRevocationNotifier
{
    /// <summary>Best effort; the data itself stays protected by the API either way.</summary>
    Task MemberRemovedAsync(Guid householdId, Guid userId, CancellationToken cancellationToken);
}
