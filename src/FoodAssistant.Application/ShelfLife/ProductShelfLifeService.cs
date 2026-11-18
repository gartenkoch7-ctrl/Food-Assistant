using FoodAssistant.Application.Common;
using FoodAssistant.Application.Diagnostics;
using FoodAssistant.Application.Households;
using FoodAssistant.Contracts.Catalog;
using FoodAssistant.Contracts.ShelfLife;
using FoodAssistant.Domain.Inventory;
using Microsoft.Extensions.Logging;

namespace FoodAssistant.Application.ShelfLife;

/// <summary>
/// Viewing and changing the product-level shelf-life overrides of the active household. A product
/// row inherits the category's effective values (household override, else shipped default). Same
/// write rules as <see cref="ShelfLifeAdminService"/> (upsert, empty override = reset).
/// </summary>
public sealed class ProductShelfLifeService(
    IShelfLifeOverrideStore store,
    IDefaultShelfLifeRules defaults,
    IHouseholdContext householdContext,
    TimeProvider time,
    ILogger<ProductShelfLifeService> logger)
{
    private const string ProductScope = "product";

    /// <summary>One row per storage type; unknown or archived product → not found.</summary>
    public async Task<OperationResult<ProductShelfLifeDto>> GetAsync(
        Guid householdId,
        Guid productId,
        CancellationToken cancellationToken)
    {
        householdContext.EnsureActive(householdId);
        var categoryKey = await store.GetCategoryOfProductAsync(productId, cancellationToken).ConfigureAwait(false);
        if (categoryKey is null)
        {
            return OperationResult<ProductShelfLifeDto>.NotFound(CatalogErrorCodes.ProductNotFound);
        }

        var own = await store.ListProductOverridesAsync(productId, cancellationToken).ConfigureAwait(false);
        var rows = new List<ShelfLifeRuleRowDto>(ShelfLifeMapping.StorageTypes.Count);
        foreach (var type in ShelfLifeMapping.StorageTypes)
        {
            rows.Add(await RowAsync(categoryKey, type, own.GetValueOrDefault(type), cancellationToken).ConfigureAwait(false));
        }

        return OperationResult<ProductShelfLifeDto>.Ok(new ProductShelfLifeDto(productId, categoryKey, rows));
    }

    public async Task<OperationResult<ShelfLifeRuleRowDto>> SetOverrideAsync(
        Guid householdId,
        Guid productId,
        string storageType,
        ShelfLifeDaysDto days,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(days);
        householdContext.EnsureActive(householdId);
        using var activity = Telemetry.Source.StartActivity("foodassistant.shelf_life.set_override");
        activity?.SetTag("foodassistant.scope", ProductScope);

        var categoryKey = await store.GetCategoryOfProductAsync(productId, cancellationToken).ConfigureAwait(false);
        if (RouteNotFound(categoryKey, storageType, out var type) is { } notFound)
        {
            activity?.SetTag("foodassistant.outcome", "not_found");
            return notFound;
        }

        var errors = ShelfLifeValidation.Validate(days);
        if (errors.Count > 0)
        {
            activity?.SetTag("foodassistant.outcome", "invalid");
            return OperationResult<ShelfLifeRuleRowDto>.Invalid(errors, ShelfLifeErrorCodes.ValidationFailed);
        }

        ShelfLifeRule? own = null;
        if (ShelfLifeMapping.IsEmpty(days))
        {
            await store.DeleteProductAsync(productId, type, cancellationToken).ConfigureAwait(false);
            ShelfLifeLog.OverrideReset(logger, householdId, ProductScope, type);
        }
        else
        {
            own = ShelfLifeMapping.ToRule(days);
            var found = await store.UpsertProductAsync(householdId, productId, type, own, time.GetUtcNow(), cancellationToken)
                .ConfigureAwait(false);
            if (!found)
            {
                // Archived between the check and the write.
                activity?.SetTag("foodassistant.outcome", "not_found");
                return OperationResult<ShelfLifeRuleRowDto>.NotFound(CatalogErrorCodes.ProductNotFound);
            }

            ShelfLifeLog.OverrideChanged(logger, householdId, ProductScope, type);
        }

        activity?.SetTag("foodassistant.outcome", "ok");
        return OperationResult<ShelfLifeRuleRowDto>.Ok(
            await RowAsync(categoryKey!, type, own, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>Removes the product override; succeeds also when there was none.</summary>
    public async Task<OperationResult<ShelfLifeRuleRowDto>> ResetOverrideAsync(
        Guid householdId,
        Guid productId,
        string storageType,
        CancellationToken cancellationToken)
    {
        householdContext.EnsureActive(householdId);
        using var activity = Telemetry.Source.StartActivity("foodassistant.shelf_life.reset_override");
        activity?.SetTag("foodassistant.scope", ProductScope);

        var categoryKey = await store.GetCategoryOfProductAsync(productId, cancellationToken).ConfigureAwait(false);
        if (RouteNotFound(categoryKey, storageType, out var type) is { } notFound)
        {
            activity?.SetTag("foodassistant.outcome", "not_found");
            return notFound;
        }

        await store.DeleteProductAsync(productId, type, cancellationToken).ConfigureAwait(false);
        ShelfLifeLog.OverrideReset(logger, householdId, ProductScope, type);
        activity?.SetTag("foodassistant.outcome", "ok");
        return OperationResult<ShelfLifeRuleRowDto>.Deleted();
    }

    private async Task<ShelfLifeRuleRowDto> RowAsync(
        string categoryKey,
        StorageType type,
        ShelfLifeRule? own,
        CancellationToken cancellationToken)
    {
        var shipped = defaults.Find(categoryKey, type);
        var categoryOverride = await store.GetForCategoryAsync(categoryKey, type, cancellationToken).ConfigureAwait(false);
        var inherited = ShelfLifeRuleResolver.Resolve(null, categoryOverride, shipped?.Rule);
        return ShelfLifeMapping.ToRow(categoryKey, type, inherited, own, ShelfLifeMapping.SourcesOf(shipped, categoryOverride));
    }

    private static OperationResult<ShelfLifeRuleRowDto>? RouteNotFound(string? categoryKey, string storageType, out StorageType type)
    {
        type = default;
        if (categoryKey is null)
        {
            return OperationResult<ShelfLifeRuleRowDto>.NotFound(CatalogErrorCodes.ProductNotFound);
        }

        return ShelfLifeMapping.TryParseStorageType(storageType, out type)
            ? null
            : OperationResult<ShelfLifeRuleRowDto>.NotFound(ShelfLifeErrorCodes.UnknownStorageType);
    }
}
