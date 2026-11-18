using FoodAssistant.Application.Catalog;
using FoodAssistant.Application.Common;
using FoodAssistant.Contracts.Catalog;
using FoodAssistant.Infrastructure.Inventory;
using FoodAssistant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FoodAssistant.Infrastructure.Catalog;

/// <summary>
/// Products and barcodes (here) and storage locations (<see cref="LocationOperations"/>) of the
/// active household. Archived products are invisible everywhere; the query filter limits all
/// reads to the active household.
/// </summary>
internal sealed class CatalogStore(AppDbContext db) : ICatalogStore
{
    private readonly LocationOperations _locations = new(db);
    private readonly BarcodeOperations _barcodes = new(db);

    public async Task<IReadOnlyList<ProductSummaryDto>> SearchAsync(
        string? query,
        ProductCursor? after,
        int take,
        CancellationToken cancellationToken)
    {
        var products = db.Set<Product>().AsNoTracking().Where(p => p.ArchivedAt == null);

        if (query is not null)
        {
            var pattern = "%" + EscapeLike(query) + "%";
            products = products.Where(p =>
                EF.Functions.ILike(p.Name, pattern, "\\") || p.Barcodes.Any(b => b.Code == query));
        }

        if (after is not null)
        {
            var (name, id) = (after.Name, after.Id);
            products = products.Where(p =>
                p.Name.CompareTo(name) > 0 || (p.Name == name && p.Id.CompareTo(id) > 0));
        }

        return await products
            .OrderBy(p => p.Name).ThenBy(p => p.Id)
            .Take(take)
            .Select(CatalogMapping.ToSummary)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<ProductDto?> GetProductAsync(Guid id, CancellationToken cancellationToken)
    {
        var product = await db.Set<Product>()
            .AsNoTracking()
            .Include(p => p.Barcodes)
            .SingleOrDefaultAsync(p => p.Id == id && p.ArchivedAt == null, cancellationToken)
            .ConfigureAwait(false);
        return product is null ? null : CatalogMapping.ToDto(product);
    }

    public async Task<ProductDto?> FindByBarcodeAsync(string code, CancellationToken cancellationToken)
    {
        var product = await db.Set<Product>()
            .AsNoTracking()
            .Include(p => p.Barcodes)
            .SingleOrDefaultAsync(p => p.ArchivedAt == null && p.Barcodes.Any(b => b.Code == code), cancellationToken)
            .ConfigureAwait(false);
        return product is null ? null : CatalogMapping.ToDto(product);
    }

    public async Task<OperationResult<ProductDto>> AddProductAsync(NewProduct product, CancellationToken cancellationToken)
    {
        if (await db.Set<Product>().AnyAsync(p => p.Id == product.Id, cancellationToken).ConfigureAwait(false))
        {
            return OperationResult<ProductDto>.Conflict(CatalogErrorCodes.ProductExists);
        }

        if (await InvalidLocationAsync(product.DefaultStorageLocationId, cancellationToken).ConfigureAwait(false) is { } invalid)
        {
            return invalid;
        }

        if (await _barcodes.InUseAsync(product.Barcodes, product.Id, cancellationToken).ConfigureAwait(false))
        {
            return OperationResult<ProductDto>.Conflict(CatalogErrorCodes.BarcodeInUse);
        }

        var entity = new Product
        {
            Id = product.Id,
            HouseholdId = product.HouseholdId,
            Name = product.Name,
            Brand = product.Brand,
            CategoryKey = product.CategoryKey,
            DefaultUnitCode = product.DefaultUnit.Code,
            DefaultStorageLocationId = product.DefaultStorageLocationId,
            SectionKey = product.SectionKey,
            MinimumStockValue = product.MinimumStock?.Value,
            MinimumStockUnitCode = product.MinimumStock?.Unit.Code,
            FoodFactsBarcode = product.FoodFactsBarcode,
            CreatedAt = product.Now,
            UpdatedAt = product.Now,
        };
        BarcodeOperations.Add(entity, product.Barcodes);
        db.Add(entity);

        try
        {
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException exception) when (PostgresErrors.IsUniqueViolation(exception))
        {
            return OperationResult<ProductDto>.Conflict(
                CatalogDbErrors.IsBarcodeViolation(exception) ? CatalogErrorCodes.BarcodeInUse : CatalogErrorCodes.ProductExists);
        }
        catch (DbUpdateException exception) when (CatalogDbErrors.IsForeignKeyViolation(exception))
        {
            // The default location was deleted between the check and the save.
            return LocationGone();
        }

        return OperationResult<ProductDto>.Created(CatalogMapping.ToDto(entity));
    }

    public async Task<OperationResult<ProductDto>> UpdateProductAsync(
        Guid id,
        long? expectedVersion,
        ProductChanges changes,
        CancellationToken cancellationToken)
    {
        var entity = await LoadAsync(id, cancellationToken).ConfigureAwait(false);
        if (entity is null)
        {
            return OperationResult<ProductDto>.NotFound(CatalogErrorCodes.ProductNotFound);
        }

        if (expectedVersion is { } expected && expected != entity.Version)
        {
            return OperationResult<ProductDto>.VersionMismatch(entity.Version, CatalogMapping.ToDto(entity));
        }

        if (await InvalidLocationAsync(changes.DefaultStorageLocationId, cancellationToken).ConfigureAwait(false) is { } invalid)
        {
            return invalid;
        }

        if (await _barcodes.InUseAsync(changes.Barcodes, id, cancellationToken).ConfigureAwait(false))
        {
            return OperationResult<ProductDto>.Conflict(CatalogErrorCodes.BarcodeInUse);
        }

        // A new minimum may be reached or not: forget that it was already put on the shopping list.
        if (entity.MinimumStockValue != changes.MinimumStock?.Value
            || entity.MinimumStockUnitCode != changes.MinimumStock?.Unit.Code)
        {
            entity.MinimumStockTriggered = false;
        }

        entity.Name = changes.Name;
        entity.Brand = changes.Brand;
        entity.CategoryKey = changes.CategoryKey;
        entity.DefaultUnitCode = changes.DefaultUnit.Code;
        entity.DefaultStorageLocationId = changes.DefaultStorageLocationId;
        entity.SectionKey = changes.SectionKey;
        entity.MinimumStockValue = changes.MinimumStock?.Value;
        entity.MinimumStockUnitCode = changes.MinimumStock?.Unit.Code;
        _barcodes.Replace(entity, changes.Barcodes);

        // Barcodes sync as part of the product, so it changes even if only they did.
        entity.UpdatedAt = changes.Now;
        if (expectedVersion is { } original)
        {
            db.Entry(entity).Property(p => p.Version).OriginalValue = (uint)original;
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException)
        {
            return await MismatchAsync(id, cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException exception) when (CatalogDbErrors.IsBarcodeViolation(exception))
        {
            return OperationResult<ProductDto>.Conflict(CatalogErrorCodes.BarcodeInUse);
        }
        catch (DbUpdateException exception) when (CatalogDbErrors.IsForeignKeyViolation(exception))
        {
            return LocationGone();
        }

        return OperationResult<ProductDto>.Ok(CatalogMapping.ToDto(entity));
    }

    public async Task<OperationResult<ProductDto>> ArchiveProductAsync(
        Guid id,
        long? expectedVersion,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var entity = await LoadAsync(id, cancellationToken).ConfigureAwait(false);
        if (entity is null)
        {
            return OperationResult<ProductDto>.NotFound(CatalogErrorCodes.ProductNotFound);
        }

        if (expectedVersion is { } expected && expected != entity.Version)
        {
            return OperationResult<ProductDto>.VersionMismatch(entity.Version, CatalogMapping.ToDto(entity));
        }

        if (await db.Set<StockBatchRecord>().AnyAsync(b => b.ProductId == id, cancellationToken).ConfigureAwait(false))
        {
            return OperationResult<ProductDto>.Conflict(CatalogErrorCodes.ProductHasStock);
        }

        // The barcodes are freed: they are unique per household, and an archived product is
        // invisible, so a new product must be able to take them over.
        // No FK guards a batch inserted concurrently with this check: batch creation (B2) must refuse archived products.
        _barcodes.Replace(entity, []);
        entity.ArchivedAt = now;
        entity.UpdatedAt = now;
        if (expectedVersion is { } original)
        {
            db.Entry(entity).Property(p => p.Version).OriginalValue = (uint)original;
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException)
        {
            return await MismatchAsync(id, cancellationToken).ConfigureAwait(false);
        }

        return OperationResult<ProductDto>.Deleted();
    }

    public Task<IReadOnlyList<StorageLocationDto>> ListLocationsAsync(CancellationToken cancellationToken) =>
        _locations.ListAsync(cancellationToken);

    public Task<OperationResult<StorageLocationDto>> AddLocationAsync(NewStorageLocation location, CancellationToken cancellationToken) =>
        _locations.AddAsync(location, cancellationToken);

    public Task<OperationResult<StorageLocationDto>> UpdateLocationAsync(
        Guid id, long? expectedVersion, StorageLocationChanges changes, CancellationToken cancellationToken) =>
        _locations.UpdateAsync(id, expectedVersion, changes, cancellationToken);

    public Task<OperationResult<StorageLocationDto>> DeleteLocationAsync(
        Guid id, long? expectedVersion, DateTimeOffset now, CancellationToken cancellationToken) =>
        _locations.DeleteAsync(id, expectedVersion, now, cancellationToken);

    // "\" is the escape character of the LIKE pattern passed to ILike above.
    private static string EscapeLike(string text) =>
        text.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);

    private Task<Product?> LoadAsync(Guid id, CancellationToken cancellationToken) =>
        db.Set<Product>()
            .Include(p => p.Barcodes)
            .SingleOrDefaultAsync(p => p.Id == id && p.ArchivedAt == null, cancellationToken);

    private async Task<OperationResult<ProductDto>?> InvalidLocationAsync(Guid? locationId, CancellationToken cancellationToken)
    {
        if (locationId is not { } id
            || await db.Set<StorageLocation>().AnyAsync(l => l.Id == id, cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        return LocationGone();
    }

    private static OperationResult<ProductDto> LocationGone() =>
        OperationResult<ProductDto>.Invalid(
            new Dictionary<string, string[]> { ["defaultStorageLocationId"] = ["not_found"] },
            CatalogErrorCodes.ValidationFailed);

    // Someone else changed the row between loading and saving: report the stored state.
    private async Task<OperationResult<ProductDto>> MismatchAsync(Guid id, CancellationToken cancellationToken)
    {
        var current = await GetProductAsync(id, cancellationToken).ConfigureAwait(false);
        return current is null
            ? OperationResult<ProductDto>.NotFound(CatalogErrorCodes.ProductNotFound)
            : OperationResult<ProductDto>.VersionMismatch(current.Version, current);
    }
}
