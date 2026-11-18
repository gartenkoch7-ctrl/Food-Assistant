using FoodAssistant.Client.Http;
using FoodAssistant.Client.Restock;
using FoodAssistant.Contracts.Restock;

namespace FoodAssistant.UI.Tests.Support;

/// <summary>Recurring-rule client over in-memory data; a wrong ETag answers 412 like the server.</summary>
public sealed class FakeRestockClient : IRestockClient
{
    public List<RecurringRuleDto> Rules { get; } = [];

    /// <summary>The requests of create and update in order, to check what the UI sent.</summary>
    public List<RecurringRuleRequest> Requests { get; } = [];

    public List<string> Calls { get; } = [];

    /// <summary>When set, every call fails with it.</summary>
    public ApiError? FailWith { get; set; }

    private ApiResult<T> Respond<T>(string call, Func<ApiResult<T>> answer)
    {
        Calls.Add(call);
        return FailWith is null ? answer() : ApiResult<T>.Failure(FailWith);
    }

    private static string ETagOf(long version) => $"\"{version}\"";

    public Task<ApiResult<IReadOnlyList<RecurringRuleDto>>> ListRulesAsync(Guid householdId, CancellationToken cancellationToken) =>
        Task.FromResult(Respond("list", () => ApiResult<IReadOnlyList<RecurringRuleDto>>.Success([.. Rules])));

    public Task<ApiResult<RecurringRuleDto>> CreateRuleAsync(
        Guid householdId, RecurringRuleRequest request, CancellationToken cancellationToken) =>
        Task.FromResult(Respond("create", () =>
        {
            Requests.Add(request);
            var rule = From(request.Id ?? Guid.CreateVersion7(), request, 1);
            Rules.Add(rule);
            return ApiResult<RecurringRuleDto>.Success(rule, ETagOf(1));
        }));

    public Task<ApiResult<RecurringRuleDto>> UpdateRuleAsync(
        Guid householdId, Guid ruleId, RecurringRuleRequest request, string etag, CancellationToken cancellationToken) =>
        Task.FromResult(Respond("update", () =>
        {
            Requests.Add(request);
            var index = Rules.FindIndex(r => r.Id == ruleId);
            if (index < 0)
            {
                return ApiResult<RecurringRuleDto>.Failure(FakeAccountClient.Error(404, RestockErrorCodes.RuleNotFound));
            }

            if (etag != ETagOf(Rules[index].Version))
            {
                return ApiResult<RecurringRuleDto>.Failure(FakeAccountClient.Error(412, "common.precondition_failed"));
            }

            var updated = From(ruleId, request, Rules[index].Version + 1);
            Rules[index] = updated;
            return ApiResult<RecurringRuleDto>.Success(updated, ETagOf(updated.Version));
        }));

    public Task<ApiResult> DeleteRuleAsync(Guid householdId, Guid ruleId, string etag, CancellationToken cancellationToken)
    {
        Calls.Add("delete");
        if (FailWith is not null)
        {
            return Task.FromResult(ApiResult.Failure(FailWith));
        }

        Rules.RemoveAll(r => r.Id == ruleId);
        return Task.FromResult(ApiResult.Success());
    }

    private static RecurringRuleDto From(Guid id, RecurringRuleRequest request, long version) => new(
        id,
        request.ListId,
        request.ProductId,
        request.Text ?? string.Empty,
        request.Quantity,
        request.Frequency,
        request.DayOfWeek,
        request.DayOfMonth,
        new DateOnly(2026, 11, 24),
        version);
}
