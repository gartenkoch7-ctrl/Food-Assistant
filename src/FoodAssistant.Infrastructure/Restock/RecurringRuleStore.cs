using FoodAssistant.Application.Common;
using FoodAssistant.Application.Households;
using FoodAssistant.Application.Restock;
using FoodAssistant.Contracts.Common;
using FoodAssistant.Contracts.Restock;
using FoodAssistant.Contracts.Shopping;
using FoodAssistant.Domain.Quantities;
using FoodAssistant.Infrastructure.Persistence;
using FoodAssistant.Infrastructure.Shopping;
using Microsoft.EntityFrameworkCore;

namespace FoodAssistant.Infrastructure.Restock;

/// <summary>
/// Recurring rules of the active household; the household filter applies to every query. Rules are
/// not synchronised (no change log), so plain saves and <c>xmin</c> as version are enough.
/// </summary>
internal sealed class RecurringRuleStore(AppDbContext db, IHouseholdContext householdContext) : IRecurringRuleStore
{
    private DbSet<RecurringShoppingRule> Rules => db.Set<RecurringShoppingRule>();

    public async Task<IReadOnlyList<RecurringRuleDto>> ListAsync(CancellationToken cancellationToken)
    {
        // Mapped after loading: PostgreSQL cannot cast xmin to bigint inside a projection.
        var rules = await Rules
            .AsNoTracking()
            .OrderBy(r => r.NextDueOn)
            .ThenBy(r => r.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return [.. rules.Select(ToDto)];
    }

    public async Task<OperationResult<RecurringRuleDto>> AddAsync(
        Guid id,
        RecurringRuleData rule,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(rule);
        if (await Rules.AnyAsync(r => r.Id == id, cancellationToken).ConfigureAwait(false))
        {
            return OperationResult<RecurringRuleDto>.Conflict(ErrorCodes.Conflict);
        }

        var entity = new RecurringShoppingRule
        {
            Id = id,
            HouseholdId = householdContext.HouseholdId
                ?? throw new InvalidOperationException("Recurring rules need an active household."),
            Text = rule.Text,
            CreatedAt = now,
        };
        Apply(entity, rule, now, scheduleChanged: true);
        Rules.Add(entity);
        try
        {
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException exception) when (ShoppingDbErrors.IsKeyConflict(exception))
        {
            db.ChangeTracker.Clear();
            return PostgresErrors.IsUniqueViolation(exception)
                ? OperationResult<RecurringRuleDto>.Conflict(ErrorCodes.Conflict)
                : GoneReference();
        }

        return OperationResult<RecurringRuleDto>.Created(ToDto(entity));
    }

    public async Task<OperationResult<RecurringRuleDto>> UpdateAsync(
        Guid ruleId,
        long? expectedVersion,
        RecurringRuleData rule,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(rule);
        var entity = await Rules.SingleOrDefaultAsync(r => r.Id == ruleId, cancellationToken).ConfigureAwait(false);
        if (entity is null)
        {
            return OperationResult<RecurringRuleDto>.NotFound(RestockErrorCodes.RuleNotFound);
        }

        if (expectedVersion is { } expected && expected != entity.Version)
        {
            return OperationResult<RecurringRuleDto>.VersionMismatch(entity.Version, ToDto(entity));
        }

        var scheduleChanged = entity.Frequency != rule.Frequency
            || entity.DayOfWeek != (int?)rule.DayOfWeek
            || entity.DayOfMonth != rule.DayOfMonth;
        Apply(entity, rule, now, scheduleChanged);
        if (expectedVersion is { } original)
        {
            // A change between loading and saving fails the UPDATE instead of being overwritten.
            db.Entry(entity).Property(r => r.Version).OriginalValue = (uint)original;
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException)
        {
            return await ReloadedMismatchAsync(ruleId, cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException exception) when (ShoppingDbErrors.IsForeignKeyViolation(exception))
        {
            db.ChangeTracker.Clear();
            return GoneReference();
        }

        return OperationResult<RecurringRuleDto>.Ok(ToDto(entity));
    }

    public async Task<OperationResult<RecurringRuleDto>> DeleteAsync(
        Guid ruleId,
        long? expectedVersion,
        CancellationToken cancellationToken)
    {
        var entity = await Rules.SingleOrDefaultAsync(r => r.Id == ruleId, cancellationToken).ConfigureAwait(false);
        if (entity is null)
        {
            return OperationResult<RecurringRuleDto>.NotFound(RestockErrorCodes.RuleNotFound);
        }

        if (expectedVersion is { } expected && expected != entity.Version)
        {
            return OperationResult<RecurringRuleDto>.VersionMismatch(entity.Version, ToDto(entity));
        }

        Rules.Remove(entity);
        if (expectedVersion is { } original)
        {
            db.Entry(entity).Property(r => r.Version).OriginalValue = (uint)original;
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException)
        {
            return await ReloadedMismatchAsync(ruleId, cancellationToken).ConfigureAwait(false);
        }

        return OperationResult<RecurringRuleDto>.Deleted();
    }

    public async Task<IReadOnlyList<DueRecurringRule>> ListDueAsync(DateOnly today, CancellationToken cancellationToken)
    {
        var rules = await Rules
            .AsNoTracking()
            .Where(r => r.NextDueOn <= today)
            .OrderBy(r => r.NextDueOn)
            .ThenBy(r => r.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return
        [
            .. rules.Select(r => new DueRecurringRule(
                r.Id,
                r.ListId,
                r.ProductId,
                r.Text,
                ToQuantity(r),
                r.Frequency,
                (DayOfWeek?)r.DayOfWeek,
                r.DayOfMonth,
                r.LastCreatedItemId,
                r.NextDueOn,
                r.Version)),
        ];
    }

    public async Task MarkRunAsync(
        Guid ruleId,
        long expectedVersion,
        DateOnly nextDueOn,
        Guid? lastCreatedItemId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        // A failed insert of a list entry may have left it tracked; it must not be saved along.
        db.ChangeTracker.Clear();
        var entity = await Rules.SingleOrDefaultAsync(r => r.Id == ruleId, cancellationToken).ConfigureAwait(false);
        if (entity is null)
        {
            return;
        }

        // Edited since the run read it: keep the editor's schedule, only remember the new entry.
        var edited = entity.Version != (uint)expectedVersion;
        if (!edited)
        {
            entity.NextDueOn = nextDueOn;
        }

        entity.LastCreatedItemId = lastCreatedItemId ?? entity.LastCreatedItemId;
        entity.UpdatedAt = now;
        try
        {
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Edited or deleted right now: the next run decides again (a deterministic entry id keeps it from adding twice).
            db.ChangeTracker.Clear();
        }
    }

    private static void Apply(RecurringShoppingRule entity, RecurringRuleData rule, DateTimeOffset now, bool scheduleChanged)
    {
        entity.ListId = rule.ListId;
        entity.ProductId = rule.ProductId;
        entity.Text = rule.Text;
        entity.QuantityValue = rule.Quantity?.Value;
        entity.QuantityUnitCode = rule.Quantity?.Unit.Code;
        entity.Frequency = rule.Frequency;
        entity.DayOfWeek = (int?)rule.DayOfWeek;
        entity.DayOfMonth = rule.DayOfMonth;
        if (scheduleChanged)
        {
            entity.NextDueOn = rule.NextDueOn;
        }

        entity.UpdatedAt = now;
    }

    private async Task<OperationResult<RecurringRuleDto>> ReloadedMismatchAsync(Guid ruleId, CancellationToken cancellationToken)
    {
        db.ChangeTracker.Clear();
        var current = await Rules.AsNoTracking().SingleOrDefaultAsync(r => r.Id == ruleId, cancellationToken).ConfigureAwait(false);
        return current is null
            ? OperationResult<RecurringRuleDto>.NotFound(RestockErrorCodes.RuleNotFound)
            : OperationResult<RecurringRuleDto>.VersionMismatch(current.Version, ToDto(current));
    }

    // The list (or product) vanished between the check and the save.
    private static OperationResult<RecurringRuleDto> GoneReference() =>
        OperationResult<RecurringRuleDto>.NotFound(ShoppingErrorCodes.ListNotFound);

    private static Quantity? ToQuantity(RecurringShoppingRule rule) =>
        rule is { QuantityValue: { } value, QuantityUnitCode: { } code } ? new Quantity(value, Unit.FromCode(code)) : null;

    private static RecurringRuleDto ToDto(RecurringShoppingRule rule) => new(
        rule.Id,
        rule.ListId,
        rule.ProductId,
        rule.Text,
        rule is { QuantityValue: { } value, QuantityUnitCode: { } code } ? new QuantityDto(value, code) : null,
        (RecurrenceFrequencyDto)(int)rule.Frequency,
        (DayOfWeek?)rule.DayOfWeek,
        rule.DayOfMonth,
        rule.NextDueOn,
        rule.Version);
}
