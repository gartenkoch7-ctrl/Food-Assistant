using FoodAssistant.Contracts.Shopping;
using FoodAssistant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FoodAssistant.Infrastructure.Shopping;

/// <summary>Turns list entities into DTOs with their open and checked entry counts (tombstones not counted).</summary>
internal sealed class ShoppingListReader(AppDbContext db)
{
    public async Task<IReadOnlyList<ShoppingListDto>> ToDtosAsync(IReadOnlyList<ShoppingList> lists, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(lists);
        var counts = await CountItemsAsync(listId: null, cancellationToken).ConfigureAwait(false);
        return [.. lists.Select(l => ToDto(l, counts))];
    }

    public async Task<ShoppingListDto> ToDtoAsync(ShoppingList list, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(list);
        return ToDto(list, await CountItemsAsync(list.Id, cancellationToken).ConfigureAwait(false));
    }

    public static ShoppingListDto ToDto(ShoppingList list, int open, int done)
    {
        ArgumentNullException.ThrowIfNull(list);
        return new(list.Id, list.Name, list.StoreId, list.IsDefault, list.SortOrder, open, done, list.Version);
    }

    private async Task<Dictionary<Guid, (int Open, int Done)>> CountItemsAsync(Guid? listId, CancellationToken cancellationToken)
    {
        var items = db.Set<ShoppingItem>().AsNoTracking();
        if (listId is { } id)
        {
            items = items.Where(i => i.ListId == id);
        }

        var counts = await items
            .GroupBy(i => i.ListId)
            .Select(g => new { ListId = g.Key, Open = g.Count(i => !i.Checked), Done = g.Count(i => i.Checked) })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return counts.ToDictionary(c => c.ListId, c => (c.Open, c.Done));
    }

    private static ShoppingListDto ToDto(ShoppingList list, Dictionary<Guid, (int Open, int Done)> counts) =>
        counts.TryGetValue(list.Id, out var count) ? ToDto(list, count.Open, count.Done) : ToDto(list, 0, 0);
}
