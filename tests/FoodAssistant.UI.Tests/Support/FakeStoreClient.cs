using FoodAssistant.Client.Http;
using FoodAssistant.Client.Stores;
using FoodAssistant.Contracts.Stores;
using FoodAssistant.UI.Catalog;

namespace FoodAssistant.UI.Tests.Support;

/// <summary>
/// Store client over in-memory data. A store's order is the learned one (<see cref="Learned"/>, default
/// order when absent) until a manual order is set; a wrong ETag answers 412 like the server.
/// </summary>
public sealed class FakeStoreClient : IStoreClient
{
    public List<StoreDto> Stores { get; } = [];

    public bool CreateConflictOnce { get; set; }
    public string? ConflictedName { get; set; } // with CreateConflictOnce: the store exists under this name

    /// <summary>The learned order per store; stores without an entry have the default order.</summary>
    public Dictionary<Guid, IReadOnlyList<string>> Learned { get; } = [];

    /// <summary>The manual order per store; present means "own order".</summary>
    public Dictionary<Guid, IReadOnlyList<string>> Manual { get; } = [];

    public List<string> Calls { get; } = [];

    /// <summary>When set, every call fails with it.</summary>
    public ApiError? FailWith { get; set; }

    private static string ETagOf(long version) => $"\"{version}\"";

    private ApiResult<T> Respond<T>(string call, Func<ApiResult<T>> answer)
    {
        Calls.Add(call);
        return FailWith is null ? answer() : ApiResult<T>.Failure(FailWith);
    }

    private SectionOrderDto OrderOf(Guid storeId) => Manual.TryGetValue(storeId, out var manual)
        ? new SectionOrderDto(storeId, manual, true)
        : new SectionOrderDto(storeId, Learned.GetValueOrDefault(storeId, SectionKeys.All), false);

    public Task<ApiResult<IReadOnlyList<StoreDto>>> ListStoresAsync(Guid householdId, CancellationToken cancellationToken) =>
        Task.FromResult(Respond("list-stores", () => ApiResult<IReadOnlyList<StoreDto>>.Success([.. Stores])));

    public Task<ApiResult<StoreDto>> CreateStoreAsync(Guid householdId, StoreRequest request, CancellationToken cancellationToken) =>
        Task.FromResult(Respond("create-store", () =>
        {
            ArgumentNullException.ThrowIfNull(request);
            var store = new StoreDto(request.Id ?? Guid.CreateVersion7(), CreateConflictOnce && ConflictedName is { } conflicted ? conflicted : request.Name, 1);
            Stores.Add(store);
            if (CreateConflictOnce)
            {
                CreateConflictOnce = false; // created, but the response is lost: the retry with the same id gets 409
                return ApiResult<StoreDto>.Failure(FakeAccountClient.Error(409, "common.conflict"));
            }

            return ApiResult<StoreDto>.Success(store, ETagOf(1));
        }));

    public Task<ApiResult<StoreDto>> UpdateStoreAsync(
        Guid householdId, Guid storeId, StoreRequest request, string etag, CancellationToken cancellationToken) =>
        Task.FromResult(Respond("update-store:" + storeId, () =>
        {
            ArgumentNullException.ThrowIfNull(request);
            var index = Stores.FindIndex(s => s.Id == storeId);
            if (index < 0)
            {
                return ApiResult<StoreDto>.Failure(FakeAccountClient.Error(404, StoreErrorCodes.StoreNotFound));
            }

            var current = Stores[index];
            if (etag != ETagOf(current.Version))
            {
                return ApiResult<StoreDto>.Failure(
                    new ApiError(412, "common.precondition_failed", null, new Dictionary<string, string[]>(), ETagOf(current.Version)));
            }

            Stores[index] = current with { Name = request.Name, Version = current.Version + 1 };
            return ApiResult<StoreDto>.Success(Stores[index], ETagOf(Stores[index].Version));
        }));

    public Task<ApiResult> DeleteStoreAsync(Guid householdId, Guid storeId, string etag, CancellationToken cancellationToken)
    {
        Calls.Add("delete-store:" + storeId);
        if (FailWith is not null)
        {
            return Task.FromResult(ApiResult.Failure(FailWith));
        }

        var current = Stores.FirstOrDefault(s => s.Id == storeId);
        if (current is null)
        {
            return Task.FromResult(ApiResult.Failure(FakeAccountClient.Error(404, StoreErrorCodes.StoreNotFound)));
        }

        Stores.Remove(current);
        return Task.FromResult(ApiResult.Success());
    }

    public Task<ApiResult<SectionOrderDto>> GetSectionOrderAsync(Guid householdId, Guid? storeId, CancellationToken cancellationToken) =>
        Task.FromResult(Respond("get-order:" + storeId, () => ApiResult<SectionOrderDto>.Success(
            storeId is { } id ? OrderOf(id) : new SectionOrderDto(null, SectionKeys.All, false))));

    public Task<ApiResult<SectionOrderDto>> SetSectionOrderAsync(
        Guid householdId, Guid storeId, SetSectionOrderRequest request, CancellationToken cancellationToken) =>
        Task.FromResult(Respond("set-order:" + storeId, () =>
        {
            ArgumentNullException.ThrowIfNull(request);
            Manual[storeId] = request.Sections;
            return ApiResult<SectionOrderDto>.Success(OrderOf(storeId));
        }));

    public Task<ApiResult<SectionOrderDto>> ResetSectionOrderAsync(Guid householdId, Guid storeId, CancellationToken cancellationToken) =>
        Task.FromResult(Respond("reset-order:" + storeId, () =>
        {
            Manual.Remove(storeId);
            return ApiResult<SectionOrderDto>.Success(OrderOf(storeId));
        }));
}
