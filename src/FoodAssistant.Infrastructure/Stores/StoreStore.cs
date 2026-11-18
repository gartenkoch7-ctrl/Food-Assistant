using FoodAssistant.Application.Common;
using FoodAssistant.Application.Households;
using FoodAssistant.Application.Stores;
using FoodAssistant.Contracts.Common;
using FoodAssistant.Contracts.Stores;
using FoodAssistant.Infrastructure.Persistence;
using FoodAssistant.Infrastructure.Shopping;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace FoodAssistant.Infrastructure.Stores;

/// <summary>Stores of the household; the active-household filter applies to every query. Store pattern of docs/kb/datenmodell.md.</summary>
internal sealed class StoreStore(AppDbContext db, IHouseholdContext householdContext) : IStoreStore
{
    private DbSet<Store> Stores => db.Set<Store>();

    private Guid ActiveHouseholdId =>
        householdContext.HouseholdId ?? throw new InvalidOperationException("Stores need an active household.");

    public async Task<IReadOnlyList<StoreDto>> ListAsync(CancellationToken cancellationToken)
    {
        // Mapped after loading: PostgreSQL cannot cast xmin to bigint inside a projection.
        var stores = await Stores
            .AsNoTracking()
            .OrderBy(s => s.Name)
            .ThenBy(s => s.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return [.. stores.Select(ToDto)];
    }

    public Task<bool> ExistsAsync(Guid storeId, CancellationToken cancellationToken) =>
        Stores.AnyAsync(s => s.Id == storeId, cancellationToken);

    public async Task<OperationResult<StoreDto>> AddAsync(Guid id, string name, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (await Stores.AnyAsync(s => s.Id == id, cancellationToken).ConfigureAwait(false))
        {
            return OperationResult<StoreDto>.Conflict(ErrorCodes.Conflict);
        }

        var store = new Store { Id = id, HouseholdId = ActiveHouseholdId, Name = name, CreatedAt = now, UpdatedAt = now };
        Stores.Add(store);
        try
        {
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException exception) when (PostgresErrors.IsUniqueViolation(exception))
        {
            // The id belongs to another household or was taken at the same moment.
            db.ChangeTracker.Clear();
            return OperationResult<StoreDto>.Conflict(ErrorCodes.Conflict);
        }

        return OperationResult<StoreDto>.Created(ToDto(store));
    }

    public async Task<OperationResult<StoreDto>> UpdateAsync(
        Guid storeId,
        long? expectedVersion,
        string name,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var store = await Stores.SingleOrDefaultAsync(s => s.Id == storeId, cancellationToken).ConfigureAwait(false);
        if (store is null)
        {
            return OperationResult<StoreDto>.NotFound(StoreErrorCodes.StoreNotFound);
        }

        if (expectedVersion is { } expected && expected != store.Version)
        {
            return OperationResult<StoreDto>.VersionMismatch(store.Version, ToDto(store));
        }

        store.Name = name;
        store.UpdatedAt = now;
        try
        {
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException)
        {
            db.ChangeTracker.Clear();
            var current = await Stores.AsNoTracking().SingleOrDefaultAsync(s => s.Id == storeId, cancellationToken).ConfigureAwait(false);
            return current is null
                ? OperationResult<StoreDto>.NotFound(StoreErrorCodes.StoreNotFound)
                : OperationResult<StoreDto>.VersionMismatch(current.Version, ToDto(current));
        }

        return OperationResult<StoreDto>.Ok(ToDto(store));
    }

    public async Task<OperationResult<StoreDto>> DeleteAsync(
        Guid storeId,
        long? expectedVersion,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var householdId = ActiveHouseholdId;
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        // Change-log lock before any row lock, in the same order as every implicit save; otherwise
        // a concurrent list insert (holding it, waiting for this row) would deadlock with us.
        await PostgresLocks.LockHouseholdChangeLogAsync(transaction.GetDbTransaction(), householdId, cancellationToken)
            .ConfigureAwait(false);
        await db.Database.ExecuteSqlAsync(
                $"SELECT 1 FROM \"Stores\" WHERE \"Id\" = {storeId} AND \"HouseholdId\" = {householdId} FOR UPDATE",
                cancellationToken)
            .ConfigureAwait(false);
        var store = await Stores.SingleOrDefaultAsync(s => s.Id == storeId, cancellationToken).ConfigureAwait(false);
        if (store is null)
        {
            return OperationResult<StoreDto>.NotFound(StoreErrorCodes.StoreNotFound);
        }

        if (expectedVersion is { } expected && expected != store.Version)
        {
            return OperationResult<StoreDto>.VersionMismatch(store.Version, ToDto(store));
        }

        // ShoppingLists.StoreId is SET NULL in the database, which would bypass the change log
        // (docs/kb/datenmodell.md): detach the lists through the context so each change is recorded.
        foreach (var list in await db.Set<ShoppingList>().Where(l => l.StoreId == storeId).ToListAsync(cancellationToken).ConfigureAwait(false))
        {
            list.StoreId = null;
            list.UpdatedAt = now;
        }

        // Section order rows are not synchronised; the database cascade may remove them.
        Stores.Remove(store);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return OperationResult<StoreDto>.Deleted();
    }

    private static StoreDto ToDto(Store store) => new(store.Id, store.Name, store.Version);
}
