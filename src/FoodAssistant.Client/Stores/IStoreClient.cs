using FoodAssistant.Client.Http;
using FoodAssistant.Contracts.Stores;

namespace FoodAssistant.Client.Stores;

/// <summary>Stores of one household and the order of their shopping sections (learned or manual).</summary>
public interface IStoreClient
{
    Task<ApiResult<IReadOnlyList<StoreDto>>> ListStoresAsync(Guid householdId, CancellationToken cancellationToken);

    Task<ApiResult<StoreDto>> CreateStoreAsync(Guid householdId, StoreRequest request, CancellationToken cancellationToken);

    Task<ApiResult<StoreDto>> UpdateStoreAsync(
        Guid householdId, Guid storeId, StoreRequest request, string etag, CancellationToken cancellationToken);

    Task<ApiResult> DeleteStoreAsync(Guid householdId, Guid storeId, string etag, CancellationToken cancellationToken);

    /// <summary>The order of one store; <c>null</c> asks for the default order that lists without a store use.</summary>
    Task<ApiResult<SectionOrderDto>> GetSectionOrderAsync(Guid householdId, Guid? storeId, CancellationToken cancellationToken);

    /// <summary>Stores the full order as the store's manual order (it then wins over the learned one).</summary>
    Task<ApiResult<SectionOrderDto>> SetSectionOrderAsync(
        Guid householdId, Guid storeId, SetSectionOrderRequest request, CancellationToken cancellationToken);

    /// <summary>Drops the manual order; the learned one applies again.</summary>
    Task<ApiResult<SectionOrderDto>> ResetSectionOrderAsync(Guid householdId, Guid storeId, CancellationToken cancellationToken);
}
