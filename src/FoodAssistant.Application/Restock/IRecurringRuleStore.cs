using FoodAssistant.Application.Common;
using FoodAssistant.Contracts.Restock;

namespace FoodAssistant.Application.Restock;

/// <summary>Recurring shopping rules of the active household (household query filter applies everywhere).</summary>
public interface IRecurringRuleStore
{
    /// <summary>All rules, ordered by next due date, then id.</summary>
    Task<IReadOnlyList<RecurringRuleDto>> ListAsync(CancellationToken cancellationToken);

    /// <summary><c>Conflict(common.conflict)</c> if the id is taken (also by another household).</summary>
    Task<OperationResult<RecurringRuleDto>> AddAsync(
        Guid id,
        RecurringRuleData rule,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    /// <summary>
    /// Store pattern (docs/kb/datenmodell.md). <see cref="RecurringRuleData.NextDueOn"/> only
    /// replaces the stored date when frequency, weekday or day of month changed.
    /// </summary>
    Task<OperationResult<RecurringRuleDto>> UpdateAsync(
        Guid ruleId,
        long? expectedVersion,
        RecurringRuleData rule,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    Task<OperationResult<RecurringRuleDto>> DeleteAsync(
        Guid ruleId,
        long? expectedVersion,
        CancellationToken cancellationToken);

    /// <summary>Rules with <c>NextDueOn &lt;= today</c>.</summary>
    Task<IReadOnlyList<DueRecurringRule>> ListDueAsync(DateOnly today, CancellationToken cancellationToken);

    /// <summary>
    /// Moves the due date on after a run and remembers the entry created last (kept when
    /// <paramref name="lastCreatedItemId"/> is <see langword="null"/>). If the rule was edited since
    /// <paramref name="expectedVersion"/> was read, the editor's schedule stays and only the created
    /// entry is remembered; a deleted rule is left alone.
    /// </summary>
    Task MarkRunAsync(
        Guid ruleId,
        long expectedVersion,
        DateOnly nextDueOn,
        Guid? lastCreatedItemId,
        DateTimeOffset now,
        CancellationToken cancellationToken);
}
