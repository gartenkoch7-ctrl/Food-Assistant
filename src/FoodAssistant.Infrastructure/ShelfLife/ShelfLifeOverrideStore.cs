using System.Linq.Expressions;
using FoodAssistant.Application.ShelfLife;
using FoodAssistant.Domain.Inventory;
using FoodAssistant.Infrastructure.Catalog;
using FoodAssistant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FoodAssistant.Infrastructure.ShelfLife;

/// <summary>Shelf-life overrides; the active-household query filter applies to every read.</summary>
internal sealed class ShelfLifeOverrideStore(AppDbContext db) : IShelfLifeOverrideStore
{
    private const int MaxWriteAttempts = 10;

    private IQueryable<ShelfLifeOverride> Overrides => db.Set<ShelfLifeOverride>();

    public async Task<ShelfLifeRule?> GetForProductAsync(Guid productId, StorageType storageType, CancellationToken cancellationToken)
    {
        var entity = await Overrides.AsNoTracking()
            .SingleOrDefaultAsync(o => o.ProductId == productId && o.StorageType == storageType, cancellationToken)
            .ConfigureAwait(false);
        return entity is null ? null : ToRule(entity);
    }

    public async Task<ShelfLifeRule?> GetForCategoryAsync(string categoryKey, StorageType storageType, CancellationToken cancellationToken)
    {
        var entity = await Overrides.AsNoTracking()
            .SingleOrDefaultAsync(o => o.CategoryKey == categoryKey && o.StorageType == storageType, cancellationToken)
            .ConfigureAwait(false);
        return entity is null ? null : ToRule(entity);
    }

    public async Task<IReadOnlyList<CategoryShelfLifeOverride>> ListCategoryOverridesAsync(CancellationToken cancellationToken)
    {
        var entities = await Overrides.AsNoTracking()
            .Where(o => o.CategoryKey != null)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return [.. entities.Select(o => new CategoryShelfLifeOverride(o.CategoryKey!, o.StorageType, ToRule(o)))];
    }

    public async Task<IReadOnlyDictionary<StorageType, ShelfLifeRule>> ListProductOverridesAsync(
        Guid productId,
        CancellationToken cancellationToken)
    {
        var entities = await Overrides.AsNoTracking()
            .Where(o => o.ProductId == productId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return entities.ToDictionary(o => o.StorageType, ToRule);
    }

    public Task UpsertCategoryAsync(
        Guid householdId,
        string categoryKey,
        StorageType storageType,
        ShelfLifeRule rule,
        DateTimeOffset now,
        CancellationToken cancellationToken) =>
        UpsertAsync(
            o => o.CategoryKey == categoryKey && o.StorageType == storageType,
            () => new ShelfLifeOverride { HouseholdId = householdId, CategoryKey = categoryKey, StorageType = storageType, CreatedAt = now },
            rule,
            now,
            cancellationToken);

    public async Task<bool> UpsertProductAsync(
        Guid householdId,
        Guid productId,
        StorageType storageType,
        ShelfLifeRule rule,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (await FindProductCategoryAsync(productId, cancellationToken).ConfigureAwait(false) is null)
        {
            return false;
        }

        await UpsertAsync(
                o => o.ProductId == productId && o.StorageType == storageType,
                () => new ShelfLifeOverride { HouseholdId = householdId, ProductId = productId, StorageType = storageType, CreatedAt = now },
                rule,
                now,
                cancellationToken)
            .ConfigureAwait(false);
        return true;
    }

    public Task DeleteCategoryAsync(string categoryKey, StorageType storageType, CancellationToken cancellationToken) =>
        DeleteAsync(o => o.CategoryKey == categoryKey && o.StorageType == storageType, cancellationToken);

    public Task DeleteProductAsync(Guid productId, StorageType storageType, CancellationToken cancellationToken) =>
        DeleteAsync(o => o.ProductId == productId && o.StorageType == storageType, cancellationToken);

    public Task<string?> GetCategoryOfProductAsync(Guid productId, CancellationToken cancellationToken) =>
        FindProductCategoryAsync(productId, cancellationToken);

    private Task<string?> FindProductCategoryAsync(Guid productId, CancellationToken cancellationToken) =>
        db.Set<Product>()
            .Where(p => p.Id == productId && p.ArchivedAt == null)
            .Select(p => p.CategoryKey)
            .SingleOrDefaultAsync(cancellationToken);

    private async Task UpsertAsync(
        Expression<Func<ShelfLifeOverride, bool>> key,
        Func<ShelfLifeOverride> create,
        ShelfLifeRule rule,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        // A write that loses a race (unique index on a parallel first insert, xmin on a parallel
        // update) reads the row again and overwrites it: last write wins, like every override write.
        // Each round lets at least one writer through, so the bound only matters under heavy contention.
        for (var attempt = 1; ; attempt++)
        {
            var entity = await db.Set<ShelfLifeOverride>().SingleOrDefaultAsync(key, cancellationToken).ConfigureAwait(false);
            if (entity is null)
            {
                entity = create();
                db.Add(entity);
            }

            Apply(entity, rule, now);
            try
            {
                await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                return;
            }
            catch (DbUpdateException exception) when (attempt < MaxWriteAttempts && IsRetryable(exception))
            {
                db.Entry(entity).State = EntityState.Detached;
            }
        }
    }

    private async Task DeleteAsync(Expression<Func<ShelfLifeOverride, bool>> key, CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            var entity = await db.Set<ShelfLifeOverride>().SingleOrDefaultAsync(key, cancellationToken).ConfigureAwait(false);
            if (entity is null)
            {
                return;
            }

            db.Remove(entity);
            try
            {
                await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                return;
            }
            catch (DbUpdateConcurrencyException) when (attempt < MaxWriteAttempts)
            {
                // Deleted or changed concurrently; a reset wants the row gone either way, so look again.
                db.Entry(entity).State = EntityState.Detached;
            }
        }
    }

    // Concurrency: the row changed or vanished between read and write; unique: a parallel insert won.
    private static bool IsRetryable(DbUpdateException exception) =>
        exception is DbUpdateConcurrencyException || PostgresErrors.IsUniqueViolation(exception);

    private static void Apply(ShelfLifeOverride entity, ShelfLifeRule rule, DateTimeOffset now)
    {
        entity.DaysAfterOpening = rule.DaysAfterOpening;
        entity.DaysFrozen = rule.DaysFrozen;
        entity.DaysAfterThawing = rule.DaysAfterThawing;
        entity.DaysAsLeftover = rule.DaysAsLeftover;
        entity.UpdatedAt = now;
    }

    private static ShelfLifeRule ToRule(ShelfLifeOverride entity) => new()
    {
        DaysAfterOpening = entity.DaysAfterOpening,
        DaysFrozen = entity.DaysFrozen,
        DaysAfterThawing = entity.DaysAfterThawing,
        DaysAsLeftover = entity.DaysAsLeftover,
    };
}
