using FoodAssistant.Application.Common;
using FoodAssistant.Application.Households;
using FoodAssistant.Application.Shopping;
using FoodAssistant.Contracts.Common;
using FoodAssistant.Contracts.Shopping;
using FoodAssistant.Infrastructure.Persistence;
using FoodAssistant.Infrastructure.Stores;
using Microsoft.EntityFrameworkCore;

namespace FoodAssistant.Infrastructure.Shopping;

/// <summary>Shopping lists; the active-household filter applies to every query. Store pattern of docs/kb/datenmodell.md.</summary>
internal sealed class ShoppingListStore(AppDbContext db, IHouseholdContext householdContext) : IShoppingListStore
{
    private DbSet<ShoppingList> Lists => db.Set<ShoppingList>();

    private Guid ActiveHouseholdId =>
        householdContext.HouseholdId ?? throw new InvalidOperationException("Shopping lists need an active household.");

    public async Task<IReadOnlyList<ShoppingListDto>> ListListsAsync(CancellationToken cancellationToken)
    {
        var lists = await Lists
            .AsNoTracking()
            .OrderBy(l => l.SortOrder)
            .ThenBy(l => l.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var counts = await CountItemsAsync(listId: null, cancellationToken).ConfigureAwait(false);
        return [.. lists.Select(l => ToDto(l, counts))];
    }

    public async Task<ShoppingListDto?> GetListAsync(Guid listId, CancellationToken cancellationToken)
    {
        var list = await Lists.AsNoTracking().SingleOrDefaultAsync(l => l.Id == listId, cancellationToken).ConfigureAwait(false);
        return list is null ? null : await ToDtoAsync(list, cancellationToken).ConfigureAwait(false);
    }

    public Task<bool> ListExistsAsync(Guid listId, CancellationToken cancellationToken) =>
        Lists.AnyAsync(l => l.Id == listId, cancellationToken);

    public async Task<OperationResult<ShoppingListDto>> AddListAsync(
        Guid id,
        string name,
        Guid? storeId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (await Lists.AnyAsync(l => l.Id == id, cancellationToken).ConfigureAwait(false))
        {
            return OperationResult<ShoppingListDto>.Conflict(ErrorCodes.Conflict);
        }

        var list = await InsertAsync(id, name, storeId, isDefault: false, now, cancellationToken).ConfigureAwait(false);
        return list is null
            ? OperationResult<ShoppingListDto>.Conflict(ErrorCodes.Conflict)
            : OperationResult<ShoppingListDto>.Created(ToDto(list, open: 0, done: 0));
    }

    public async Task<OperationResult<ShoppingListDto>> UpdateListAsync(
        Guid listId,
        long? expectedVersion,
        UpdateShoppingListRequest changes,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(changes);
        var list = await Lists.SingleOrDefaultAsync(l => l.Id == listId, cancellationToken).ConfigureAwait(false);
        if (list is null)
        {
            return OperationResult<ShoppingListDto>.NotFound(ShoppingErrorCodes.ListNotFound);
        }

        if (expectedVersion is { } expected && expected != list.Version)
        {
            return await MismatchAsync(list, cancellationToken).ConfigureAwait(false);
        }

        if (list.IsDefault && !changes.IsDefault)
        {
            return OperationResult<ShoppingListDto>.Conflict(ShoppingErrorCodes.DefaultList);
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (changes.IsDefault && !list.IsDefault)
            {
                // Two statements: the unique index allows one default per household at any moment,
                // so the old default must lose its flag before the new one gets it.
                foreach (var previous in await Lists.Where(l => l.IsDefault).ToListAsync(cancellationToken).ConfigureAwait(false))
                {
                    previous.IsDefault = false;
                    previous.UpdatedAt = now;
                }

                await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }

            list.Name = changes.Name;
            list.StoreId = changes.StoreId;
            list.IsDefault = changes.IsDefault;
            list.SortOrder = changes.SortOrder;
            list.UpdatedAt = now;
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException exception) when (exception is DbUpdateConcurrencyException || PostgresErrors.IsUniqueViolation(exception))
        {
            // Changed meanwhile (or another list became default at the same moment).
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return await ReloadedMismatchAsync(listId, cancellationToken).ConfigureAwait(false);
        }

        return OperationResult<ShoppingListDto>.Ok(await ToDtoAsync(list, cancellationToken).ConfigureAwait(false));
    }

    public async Task<OperationResult<ShoppingListDto>> DeleteListAsync(
        Guid listId,
        long? expectedVersion,
        CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        // Blocks new entries for this list until the commit (their foreign-key check waits for the
        // row lock), so none can slip past the change log through the database cascade below.
        await db.Database.ExecuteSqlAsync(
                $"SELECT 1 FROM \"ShoppingLists\" WHERE \"Id\" = {listId} AND \"HouseholdId\" = {ActiveHouseholdId} FOR UPDATE",
                cancellationToken)
            .ConfigureAwait(false);
        var list = await Lists.SingleOrDefaultAsync(l => l.Id == listId, cancellationToken).ConfigureAwait(false);
        if (list is null)
        {
            return OperationResult<ShoppingListDto>.NotFound(ShoppingErrorCodes.ListNotFound);
        }

        if (expectedVersion is { } expected && expected != list.Version)
        {
            return await MismatchAsync(list, cancellationToken).ConfigureAwait(false);
        }

        if (list.IsDefault)
        {
            return OperationResult<ShoppingListDto>.Conflict(ShoppingErrorCodes.DefaultList);
        }

        // The cascade ShoppingItems.ListId would bypass the change log (docs/kb/datenmodell.md):
        // delete the entries, tombstones included, through the context so each one is recorded.
        var items = await db.Set<ShoppingItem>()
            .IgnoreQueryFilters([AppDbContext.NotDeletedFilter])
            .Where(i => i.ListId == listId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        db.RemoveRange(items);
        Lists.Remove(list);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return OperationResult<ShoppingListDto>.Deleted();
    }

    public async Task<ShoppingListDto> EnsureDefaultListAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var current = await Lists.AsNoTracking().SingleOrDefaultAsync(l => l.IsDefault, cancellationToken).ConfigureAwait(false);
        if (current is null)
        {
            // Empty name: the UI shows "Einkaufsliste"/"Shopping list" in the reader's language.
            current = await InsertAsync(Guid.CreateVersion7(), string.Empty, storeId: null, isDefault: true, now, cancellationToken)
                .ConfigureAwait(false);

            // Null: a concurrent request created the default first (unique index); use that one.
            current ??= await Lists.AsNoTracking().SingleAsync(l => l.IsDefault, cancellationToken).ConfigureAwait(false);
        }

        return await ToDtoAsync(current, cancellationToken).ConfigureAwait(false);
    }

    public Task<bool> StoreExistsAsync(Guid storeId, CancellationToken cancellationToken) =>
        db.Set<Store>().AnyAsync(s => s.Id == storeId, cancellationToken);

    public Task<Guid?> GetStoreIdOfListAsync(Guid listId, CancellationToken cancellationToken) =>
        Lists.Where(l => l.Id == listId).Select(l => l.StoreId).SingleOrDefaultAsync(cancellationToken);

    // Null if a unique index refused the row (id taken, or a second default list).
    private async Task<ShoppingList?> InsertAsync(
        Guid id,
        string name,
        Guid? storeId,
        bool isDefault,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var last = await Lists.MaxAsync(l => (int?)l.SortOrder, cancellationToken).ConfigureAwait(false);
        var list = new ShoppingList
        {
            Id = id,
            HouseholdId = ActiveHouseholdId,
            Name = name,
            StoreId = storeId,
            IsDefault = isDefault,
            SortOrder = (last ?? -1) + 1,
            CreatedAt = now,
            UpdatedAt = now,
        };
        Lists.Add(list);
        try
        {
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException exception) when (PostgresErrors.IsUniqueViolation(exception))
        {
            db.ChangeTracker.Clear();
            return null;
        }

        return list;
    }

    private async Task<OperationResult<ShoppingListDto>> MismatchAsync(ShoppingList list, CancellationToken cancellationToken) =>
        OperationResult<ShoppingListDto>.VersionMismatch(list.Version, await ToDtoAsync(list, cancellationToken).ConfigureAwait(false));

    private async Task<OperationResult<ShoppingListDto>> ReloadedMismatchAsync(Guid listId, CancellationToken cancellationToken)
    {
        db.ChangeTracker.Clear();
        var current = await Lists.AsNoTracking().SingleOrDefaultAsync(l => l.Id == listId, cancellationToken).ConfigureAwait(false);
        return current is null
            ? OperationResult<ShoppingListDto>.NotFound(ShoppingErrorCodes.ListNotFound)
            : await MismatchAsync(current, cancellationToken).ConfigureAwait(false);
    }

    private async Task<ShoppingListDto> ToDtoAsync(ShoppingList list, CancellationToken cancellationToken) =>
        ToDto(list, await CountItemsAsync(list.Id, cancellationToken).ConfigureAwait(false));

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

    private static ShoppingListDto ToDto(ShoppingList list, int open, int done) =>
        new(list.Id, list.Name, list.StoreId, list.IsDefault, list.SortOrder, open, done, list.Version);
}
