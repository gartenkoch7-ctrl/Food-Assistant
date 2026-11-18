using FoodAssistant.Domain.Quantities;
using FoodAssistant.Domain.Shopping;

namespace FoodAssistant.Application.Shopping;

/// <summary>Pure steps of changing a shopping entry after the conflict rules have decided.</summary>
internal static class ShoppingItemTransitions
{
    /// <summary>
    /// The stored entry after the incoming state won. Text and section follow "last change wins"
    /// like quantity and note: they are taken only when the incoming state wins as a whole.
    /// Check-off time and person change only on a transition; a won change revives a tombstone.
    /// </summary>
    public static ShoppingItemRecord WithIncoming(
        ShoppingItemRecord server,
        ShoppingItemState winner,
        string text,
        string sectionKey,
        Guid userId,
        DateTimeOffset now)
    {
        var keepsCheck = winner.Checked && server.Checked;
        return server with
        {
            Text = text,
            SectionKey = sectionKey,
            Quantity = winner.Quantity,
            Note = winner.Note,
            Checked = winner.Checked,
            CheckedAt = winner.Checked ? (keepsCheck ? server.CheckedAt : now) : null,
            CheckedByUserId = winner.Checked ? (keepsCheck ? server.CheckedByUserId : userId) : null,
            Version = winner.Version,
            UpdatedAt = now,
            DeletedAt = null,
        };
    }

    /// <summary>
    /// Restricted members may only check off (ADR-0002). Compared with the current server state,
    /// so a stale but otherwise unchanged snapshot still counts as "only checked".
    /// </summary>
    public static bool HasSameContent(ShoppingItemRecord server, string text, string sectionKey, Quantity? quantity, string? note) =>
        string.Equals(server.Text, text, StringComparison.Ordinal)
        && string.Equals(server.SectionKey, sectionKey, StringComparison.Ordinal)
        && Nullable.Equals(server.Quantity, quantity)
        && string.Equals(server.Note, note, StringComparison.Ordinal);
}
