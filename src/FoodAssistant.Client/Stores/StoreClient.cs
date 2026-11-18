using FoodAssistant.Client.Http;
using FoodAssistant.Contracts.Stores;

namespace FoodAssistant.Client.Stores;

/// <summary>The stores API of a household over <see cref="ApiHttpClient"/>.</summary>
public sealed class StoreClient(ApiHttpClient api) : IStoreClient
{
    public Task<ApiResult<IReadOnlyList<StoreDto>>> ListStoresAsync(Guid householdId, CancellationToken cancellationToken) =>
        api.GetAsync<IReadOnlyList<StoreDto>>(ApiPaths.Household(householdId, "stores"), cancellationToken);

    public Task<ApiResult<StoreDto>> CreateStoreAsync(Guid householdId, StoreRequest request, CancellationToken cancellationToken) =>
        api.SendAsync<StoreRequest, StoreDto>(
            HttpMethod.Post, ApiPaths.Household(householdId, "stores"), request, null, null, cancellationToken);

    public Task<ApiResult<StoreDto>> UpdateStoreAsync(
        Guid householdId, Guid storeId, StoreRequest request, string etag, CancellationToken cancellationToken) =>
        api.SendAsync<StoreRequest, StoreDto>(
            HttpMethod.Put, ApiPaths.Household(householdId, $"stores/{storeId:D}"), request, etag, null, cancellationToken);

    public Task<ApiResult> DeleteStoreAsync(Guid householdId, Guid storeId, string etag, CancellationToken cancellationToken) =>
        api.SendAsync<object>(
            HttpMethod.Delete, ApiPaths.Household(householdId, $"stores/{storeId:D}"), null, etag, null, cancellationToken);

    public Task<ApiResult<SectionOrderDto>> GetSectionOrderAsync(Guid householdId, Guid? storeId, CancellationToken cancellationToken) =>
        api.GetAsync<SectionOrderDto>(
            ApiPaths.Household(householdId, storeId is { } id ? $"stores/{id:D}/section-order" : "shopping/sections"),
            cancellationToken);

    public Task<ApiResult<SectionOrderDto>> SetSectionOrderAsync(
        Guid householdId, Guid storeId, SetSectionOrderRequest request, CancellationToken cancellationToken) =>
        api.SendAsync<SetSectionOrderRequest, SectionOrderDto>(
            HttpMethod.Put, ApiPaths.Household(householdId, $"stores/{storeId:D}/section-order"), request, null, null, cancellationToken);

    public Task<ApiResult<SectionOrderDto>> ResetSectionOrderAsync(Guid householdId, Guid storeId, CancellationToken cancellationToken) =>
        api.SendAsync<object, SectionOrderDto>(
            HttpMethod.Delete, ApiPaths.Household(householdId, $"stores/{storeId:D}/section-order"), null, null, null, cancellationToken);
}
