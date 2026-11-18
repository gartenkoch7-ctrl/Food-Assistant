using FoodAssistant.Application.Catalog;
using FoodAssistant.Application.Common;
using FoodAssistant.Contracts.Catalog;
using FoodAssistant.Infrastructure.Inventory;
using FoodAssistant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FoodAssistant.Infrastructure.Catalog;

/// <summary>Storage location part of <see cref="CatalogStore"/>; the active-household filter applies.</summary>
internal sealed class LocationOperations(AppDbContext db)
{
    public async Task<IReadOnlyList<StorageLocationDto>> ListAsync(CancellationToken cancellationToken)
    {
        var locations = await db.Set<StorageLocation>()
            .AsNoTracking()
            .OrderBy(l => l.SortOrder).ThenBy(l => l.Name).ThenBy(l => l.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return [.. locations.Select(CatalogMapping.ToDto)];
    }

    public async Task<OperationResult<StorageLocationDto>> AddAsync(NewStorageLocation location, CancellationToken cancellationToken)
    {
        if (await db.Set<StorageLocation>().AnyAsync(l => l.Id == location.Id, cancellationToken).ConfigureAwait(false))
        {
            return OperationResult<StorageLocationDto>.Conflict(CatalogErrorCodes.LocationExists);
        }

        var sortOrder = location.SortOrder
            ?? (await db.Set<StorageLocation>().Select(l => (int?)l.SortOrder).MaxAsync(cancellationToken).ConfigureAwait(false) ?? -1) + 1;
        var entity = new StorageLocation
        {
            Id = location.Id,
            HouseholdId = location.HouseholdId,
            Name = location.Name,
            Type = location.Type,
            SortOrder = sortOrder,
            CreatedAt = location.Now,
            UpdatedAt = location.Now,
        };
        db.Add(entity);
        try
        {
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException exception) when (PostgresErrors.IsUniqueViolation(exception))
        {
            // Same id sent twice at the same moment (or an id of another household).
            return OperationResult<StorageLocationDto>.Conflict(CatalogErrorCodes.LocationExists);
        }

        return OperationResult<StorageLocationDto>.Created(CatalogMapping.ToDto(entity));
    }

    public async Task<OperationResult<StorageLocationDto>> UpdateAsync(
        Guid id,
        long? expectedVersion,
        StorageLocationChanges changes,
        CancellationToken cancellationToken)
    {
        var entity = await db.Set<StorageLocation>().SingleOrDefaultAsync(l => l.Id == id, cancellationToken).ConfigureAwait(false);
        if (entity is null)
        {
            return OperationResult<StorageLocationDto>.NotFound(CatalogErrorCodes.LocationNotFound);
        }

        if (expectedVersion is { } expected && expected != entity.Version)
        {
            return OperationResult<StorageLocationDto>.VersionMismatch(entity.Version, CatalogMapping.ToDto(entity));
        }

        if (entity.Type != changes.Type && await HasBatchesAsync(id, cancellationToken).ConfigureAwait(false))
        {
            return OperationResult<StorageLocationDto>.Conflict(CatalogErrorCodes.LocationTypeLocked);
        }

        entity.Name = changes.Name;
        entity.Type = changes.Type;
        entity.SortOrder = changes.SortOrder ?? entity.SortOrder;
        entity.UpdatedAt = changes.Now;
        if (expectedVersion is { } original)
        {
            db.Entry(entity).Property(l => l.Version).OriginalValue = (uint)original;
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException)
        {
            return await MismatchAsync(id, cancellationToken).ConfigureAwait(false);
        }

        return OperationResult<StorageLocationDto>.Ok(CatalogMapping.ToDto(entity));
    }

    public async Task<OperationResult<StorageLocationDto>> DeleteAsync(
        Guid id,
        long? expectedVersion,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var entity = await db.Set<StorageLocation>().SingleOrDefaultAsync(l => l.Id == id, cancellationToken).ConfigureAwait(false);
        if (entity is null)
        {
            return OperationResult<StorageLocationDto>.NotFound(CatalogErrorCodes.LocationNotFound);
        }

        if (expectedVersion is { } expected && expected != entity.Version)
        {
            return OperationResult<StorageLocationDto>.VersionMismatch(entity.Version, CatalogMapping.ToDto(entity));
        }

        if (await HasBatchesAsync(id, cancellationToken).ConfigureAwait(false))
        {
            return OperationResult<StorageLocationDto>.Conflict(CatalogErrorCodes.LocationInUse);
        }

        // The database would set the column to null on delete, but that bypasses the change log
        // (docs/kb/datenmodell.md): change the products through the context so clients see it.
        var products = await db.Set<Product>()
            .Where(p => p.DefaultStorageLocationId == id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        foreach (var product in products)
        {
            product.DefaultStorageLocationId = null;
            product.UpdatedAt = now;
        }

        db.Remove(entity);
        if (expectedVersion is { } original)
        {
            db.Entry(entity).Property(l => l.Version).OriginalValue = (uint)original;
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException)
        {
            return await MismatchAsync(id, cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException exception) when (CatalogDbErrors.IsForeignKeyViolation(exception))
        {
            // A batch was placed here between the check and the delete.
            return OperationResult<StorageLocationDto>.Conflict(CatalogErrorCodes.LocationInUse);
        }

        return OperationResult<StorageLocationDto>.Deleted();
    }

    private Task<bool> HasBatchesAsync(Guid locationId, CancellationToken cancellationToken) =>
        db.Set<StockBatchRecord>().AnyAsync(b => b.StorageLocationId == locationId, cancellationToken);

    // Someone else changed or deleted the row between loading and saving: report the stored state.
    private async Task<OperationResult<StorageLocationDto>> MismatchAsync(Guid id, CancellationToken cancellationToken)
    {
        var current = await db.Set<StorageLocation>().AsNoTracking().SingleOrDefaultAsync(l => l.Id == id, cancellationToken).ConfigureAwait(false);
        return current is null
            ? OperationResult<StorageLocationDto>.NotFound(CatalogErrorCodes.LocationNotFound)
            : OperationResult<StorageLocationDto>.VersionMismatch(current.Version, CatalogMapping.ToDto(current));
    }
}
