using FoodAssistant.Contracts.Shopping;

namespace FoodAssistant.UI.Shopping;

/// <summary>
/// Items this person is changing right now. The server publishes a change over SignalR before it
/// writes the HTTP response, so the live message of an own check-off usually arrives while the request
/// is still open; such changes must not be announced as "somebody else" did them.
/// </summary>
internal sealed class OwnItemChanges(TimeProvider time)
{
    private static readonly TimeSpan Window = TimeSpan.FromSeconds(15);

    private readonly Dictionary<Guid, DateTimeOffset> _until = [];

    public void Mark(Guid itemId)
    {
        var now = time.GetUtcNow();
        foreach (var expired in _until.Where(e => e.Value <= now).Select(e => e.Key).ToList())
        {
            _until.Remove(expired);
        }

        _until[itemId] = now + Window;
    }

    public bool IsOwn(Guid itemId) => _until.TryGetValue(itemId, out var until) && until > time.GetUtcNow();

    /// <summary>
    /// Items that were open in <paramref name="before"/> and are checked in <paramref name="loaded"/>: someone else
    /// checked them, unless this person is checking them right now.
    /// </summary>
    public IEnumerable<ShoppingItemDto> ForeignCheckOffs(IEnumerable<ShoppingItemDto> before, IEnumerable<ShoppingItemDto> loaded)
    {
        var old = before.ToDictionary(i => i.Id);
        return [.. loaded.Where(i => i.Checked && !IsOwn(i.Id) && old.TryGetValue(i.Id, out var was) && !was.Checked)];
    }
}
