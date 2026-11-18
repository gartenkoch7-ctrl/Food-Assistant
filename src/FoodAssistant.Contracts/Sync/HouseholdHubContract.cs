namespace FoodAssistant.Contracts.Sync;

/// <summary>Path and method names of the SignalR hub for live updates (ADR-0005).</summary>
public static class HouseholdHubContract
{
    /// <summary>Hub path, relative to the server root.</summary>
    public const string Path = "/hubs/household";

    /// <summary>Client → server: join the group of one of the caller's households.</summary>
    public const string Join = "Join";

    /// <summary>Client → server: leave a household group.</summary>
    public const string Leave = "Leave";

    /// <summary>Server → client: <see cref="HouseholdChangesMessage"/> after a commit.</summary>
    public const string Changed = "Changed";
}
