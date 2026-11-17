namespace FoodAssistant.Domain.Shopping;

/// <summary>
/// Resolves an incoming shopping list change against the stored item without asking the user,
/// by the conflict table of ADR-0005.
/// </summary>
/// <remarks>
/// <para>
/// A conflict exists when the incoming change is based on an older version than the stored one.
/// Only snapshots are compared, so the resolver cannot tell which fields a side touched. Each rule
/// therefore picks one side as a whole; the winning snapshot gets the next version and the arrival
/// time of the incoming change.
/// </para>
/// <para>Order: deletion rules, then "checked wins", then "last change wins".</para>
/// <list type="bullet">
/// <item>Deleted vs. changed or checked: the side that still has the item wins (a queued change is
/// only sent when the client changed something; a newer server version means someone did).</item>
/// <item>Checked vs. unchecked: the checked side wins with its quantity and note, because that is
/// what was bought ("gekauft ist gekauft").</item>
/// <item>Otherwise the change the server received last wins (<see cref="ShoppingItemState.UpdatedAt"/>);
/// on equal times the incoming one, since it arrived after the stored one.</item>
/// </list>
/// </remarks>
public static class ShoppingConflictResolver
{
    /// <summary>Resolves <paramref name="incoming"/> against the stored <paramref name="server"/> state.</summary>
    /// <param name="server">Stored state including tombstones, or null if the server never stored the item.</param>
    /// <param name="incoming">Client snapshot after its change, carrying the version it was based on.</param>
    /// <exception cref="ArgumentException">
    /// The ids differ, or the incoming version is newer than the stored one.
    /// </exception>
    public static ShoppingResolution Resolve(ShoppingItemState? server, ShoppingItemState incoming)
    {
        ArgumentNullException.ThrowIfNull(incoming);
        if (server is null)
        {
            // New items carry client-generated UUIDv7 ids, so two new entries for the same article
            // never collide: each one is created and both are kept.
            return Accept(incoming, incoming.Version, ShoppingConflictOutcome.Created);
        }

        if (server.Id != incoming.Id)
        {
            throw new ArgumentException("The incoming change belongs to a different item.", nameof(incoming));
        }

        if (incoming.Version > server.Version)
        {
            throw new ArgumentException("The incoming change is based on an unknown version.", nameof(incoming));
        }

        if (incoming.Version == server.Version)
        {
            return Accept(incoming, server.Version, ShoppingConflictOutcome.NoConflict);
        }

        return ResolveConflict(server, incoming);
    }

    private static ShoppingResolution ResolveConflict(ShoppingItemState server, ShoppingItemState incoming)
    {
        if (server.Deleted && incoming.Deleted)
        {
            return Keep(server, ShoppingConflictOutcome.BothDeleted);
        }

        if (server.Deleted || incoming.Deleted)
        {
            var survivor = server.Deleted ? incoming : server;
            var outcome = survivor.Checked
                ? ShoppingConflictOutcome.CheckedWinsOverDelete
                : ShoppingConflictOutcome.ChangeWinsOverDelete;
            return Pick(server, incoming, incomingWins: server.Deleted, outcome);
        }

        if (server.Checked != incoming.Checked)
        {
            return Pick(server, incoming, incomingWins: incoming.Checked, ShoppingConflictOutcome.CheckedWins);
        }

        return Pick(
            server,
            incoming,
            incomingWins: incoming.UpdatedAt >= server.UpdatedAt,
            ShoppingConflictOutcome.LastWriteWins);
    }

    private static ShoppingResolution Pick(
        ShoppingItemState server,
        ShoppingItemState incoming,
        bool incomingWins,
        ShoppingConflictOutcome outcome) =>
        incomingWins ? Accept(incoming, server.Version, outcome) : Keep(server, outcome);

    private static ShoppingResolution Accept(ShoppingItemState incoming, long previousVersion, ShoppingConflictOutcome outcome) =>
        new(incoming with { Version = previousVersion + 1 }, outcome, ServerChanged: true);

    private static ShoppingResolution Keep(ShoppingItemState server, ShoppingConflictOutcome outcome) =>
        new(server, outcome, ServerChanged: false);
}
