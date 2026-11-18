using FoodAssistant.Client.Http;
using FoodAssistant.Contracts.Restock;

namespace FoodAssistant.Client.Restock;

/// <summary>Recurring shopping rules of one household.</summary>
public interface IRestockClient
{
    Task<ApiResult<IReadOnlyList<RecurringRuleDto>>> ListRulesAsync(Guid householdId, CancellationToken cancellationToken);

    Task<ApiResult<RecurringRuleDto>> CreateRuleAsync(
        Guid householdId, RecurringRuleRequest request, CancellationToken cancellationToken);

    Task<ApiResult<RecurringRuleDto>> UpdateRuleAsync(
        Guid householdId, Guid ruleId, RecurringRuleRequest request, string etag, CancellationToken cancellationToken);

    Task<ApiResult> DeleteRuleAsync(Guid householdId, Guid ruleId, string etag, CancellationToken cancellationToken);
}
