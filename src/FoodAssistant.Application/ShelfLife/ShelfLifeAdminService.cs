using FoodAssistant.Application.Common;
using FoodAssistant.Application.Diagnostics;
using FoodAssistant.Application.Households;
using FoodAssistant.Contracts.ShelfLife;
using FoodAssistant.Domain.Catalog;
using FoodAssistant.Domain.Inventory;
using Microsoft.Extensions.Logging;

namespace FoodAssistant.Application.ShelfLife;

/// <summary>
/// Viewing and changing the category-level shelf-life rules of the active household: shipped
/// defaults plus the household's overrides. Values only come from checked sources or the household
/// itself, never from the AI (AGENTS.md 2.3). Product overrides: <see cref="ProductShelfLifeService"/>.
/// </summary>
/// <remarks>
/// Overrides are upserts without <c>If-Match</c>: the request carries the full set of values for one
/// row, so "last write wins" loses nothing a user could not see, and the row has no other state.
/// An override without any value is no override: saving one removes it.
/// </remarks>
public sealed class ShelfLifeAdminService(
    IShelfLifeOverrideStore store,
    IDefaultShelfLifeRules defaults,
    IHouseholdContext householdContext,
    TimeProvider time,
    ILogger<ShelfLifeAdminService> logger)
{
    private const string CategoryScope = "category";

    /// <summary>All categories × storage types, with or without default.</summary>
    public async Task<IReadOnlyList<ShelfLifeRuleRowDto>> ListRulesAsync(Guid householdId, CancellationToken cancellationToken)
    {
        householdContext.EnsureActive(householdId);
        var overrides = (await store.ListCategoryOverridesAsync(cancellationToken).ConfigureAwait(false))
            .ToDictionary(o => (o.CategoryKey, o.StorageType), o => o.Rule);

        return
        [
            .. from category in ProductCategories.All
               from storageType in ShelfLifeMapping.StorageTypes
               select CategoryRow(category, storageType, overrides.GetValueOrDefault((category, storageType))),
        ];
    }

    /// <summary>Sources cited by the shipped defaults, ordered by key.</summary>
    public IReadOnlyList<ShelfLifeSourceDto> ListSources() =>
    [
        .. defaults.Sources.Values
            .OrderBy(s => s.Key, StringComparer.Ordinal)
            .Select(s => new ShelfLifeSourceDto(s.Key, s.Title, s.Url.ToString(), s.Retrieved)),
    ];

    public async Task<OperationResult<ShelfLifeRuleRowDto>> SetCategoryOverrideAsync(
        Guid householdId,
        string categoryKey,
        string storageType,
        ShelfLifeDaysDto days,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(days);
        householdContext.EnsureActive(householdId);
        using var activity = Telemetry.Source.StartActivity("foodassistant.shelf_life.set_override");
        activity?.SetTag("foodassistant.scope", CategoryScope);

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
            await store.DeleteCategoryAsync(categoryKey, type, cancellationToken).ConfigureAwait(false);
            ShelfLifeLog.OverrideReset(logger, householdId, CategoryScope, type);
        }
        else
        {
            own = ShelfLifeMapping.ToRule(days);
            await store.UpsertCategoryAsync(householdId, categoryKey, type, own, time.GetUtcNow(), cancellationToken)
                .ConfigureAwait(false);
            ShelfLifeLog.OverrideChanged(logger, householdId, CategoryScope, type);
        }

        activity?.SetTag("foodassistant.outcome", "ok");
        return OperationResult<ShelfLifeRuleRowDto>.Ok(CategoryRow(categoryKey, type, own));
    }

    /// <summary>Removes the category override; succeeds also when there was none.</summary>
    public async Task<OperationResult<ShelfLifeRuleRowDto>> ResetCategoryOverrideAsync(
        Guid householdId,
        string categoryKey,
        string storageType,
        CancellationToken cancellationToken)
    {
        householdContext.EnsureActive(householdId);
        using var activity = Telemetry.Source.StartActivity("foodassistant.shelf_life.reset_override");
        activity?.SetTag("foodassistant.scope", CategoryScope);

        if (RouteNotFound(categoryKey, storageType, out var type) is { } notFound)
        {
            activity?.SetTag("foodassistant.outcome", "not_found");
            return notFound;
        }

        await store.DeleteCategoryAsync(categoryKey, type, cancellationToken).ConfigureAwait(false);
        ShelfLifeLog.OverrideReset(logger, householdId, CategoryScope, type);
        activity?.SetTag("foodassistant.outcome", "ok");
        return OperationResult<ShelfLifeRuleRowDto>.Deleted();
    }

    private ShelfLifeRuleRowDto CategoryRow(string categoryKey, StorageType type, ShelfLifeRule? own)
    {
        var shipped = defaults.Find(categoryKey, type);
        return ShelfLifeMapping.ToRow(categoryKey, type, shipped?.Rule ?? ShelfLifeRule.Empty, own, shipped?.SourceKey);
    }

    private static OperationResult<ShelfLifeRuleRowDto>? RouteNotFound(string categoryKey, string storageType, out StorageType type)
    {
        type = default;
        if (!ProductCategories.IsKnown(categoryKey))
        {
            return OperationResult<ShelfLifeRuleRowDto>.NotFound(ShelfLifeErrorCodes.UnknownCategory);
        }

        return ShelfLifeMapping.TryParseStorageType(storageType, out type)
            ? null
            : OperationResult<ShelfLifeRuleRowDto>.NotFound(ShelfLifeErrorCodes.UnknownStorageType);
    }
}
