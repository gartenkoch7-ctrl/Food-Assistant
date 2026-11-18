using FoodAssistant.Client.Http;
using FoodAssistant.Contracts.Restock;

namespace FoodAssistant.Client.Restock;

/// <summary>The recurring-rules API of a household over <see cref="ApiHttpClient"/>.</summary>
public sealed class RestockClient(ApiHttpClient api) : IRestockClient
{
    public Task<ApiResult<IReadOnlyList<RecurringRuleDto>>> ListRulesAsync(Guid householdId, CancellationToken cancellationToken) =>
        api.GetAsync<IReadOnlyList<RecurringRuleDto>>(ApiPaths.Household(householdId, "shopping/recurring"), cancellationToken);

    public Task<ApiResult<RecurringRuleDto>> CreateRuleAsync(
        Guid householdId, RecurringRuleRequest request, CancellationToken cancellationToken) =>
        api.SendAsync<RecurringRuleRequest, RecurringRuleDto>(
            HttpMethod.Post, ApiPaths.Household(householdId, "shopping/recurring"), request, null, null, cancellationToken);

    public Task<ApiResult<RecurringRuleDto>> UpdateRuleAsync(
        Guid householdId, Guid ruleId, RecurringRuleRequest request, string etag, CancellationToken cancellationToken) =>
        api.SendAsync<RecurringRuleRequest, RecurringRuleDto>(
            HttpMethod.Put, ApiPaths.Household(householdId, $"shopping/recurring/{ruleId:D}"), request, etag, null, cancellationToken);

    public Task<ApiResult> DeleteRuleAsync(Guid householdId, Guid ruleId, string etag, CancellationToken cancellationToken) =>
        api.SendAsync<object>(
            HttpMethod.Delete, ApiPaths.Household(householdId, $"shopping/recurring/{ruleId:D}"), null, etag, null, cancellationToken);
}
