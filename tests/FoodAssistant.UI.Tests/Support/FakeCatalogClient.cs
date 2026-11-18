using FoodAssistant.Client.Catalog;
using FoodAssistant.Client.Http;
using FoodAssistant.Contracts.Catalog;
using FoodAssistant.Contracts.Common;

namespace FoodAssistant.UI.Tests.Support;

/// <summary>
/// Catalog client over in-memory lists, used by the UI tests of the catalog, inventory, shopping and
/// store features. Versions increase on every change; a wrong ETag answers 412 like the server.
/// </summary>
public sealed class FakeCatalogClient : ICatalogClient
{
    public List<ProductDto> Products { get; } = [];

    public List<StorageLocationDto> Locations { get; } = [];

    public List<CategoryDto> Categories { get; } = [];

    public List<string> Calls { get; } = [];

    /// <summary>When set, every call fails with it (network error, 403 …).</summary>
    public ApiError? FailWith { get; set; }

    private static string ETagOf(long version) => $"\"{version}\"";

    private ApiResult<T> Respond<T>(string call, Func<ApiResult<T>> answer)
    {
        Calls.Add(call);
        return FailWith is null ? answer() : ApiResult<T>.Failure(FailWith);
    }

    private ApiResult RespondEmpty(string call, Func<ApiResult> answer)
    {
        Calls.Add(call);
        return FailWith is null ? answer() : ApiResult.Failure(FailWith);
    }

    private static ApiError PreconditionFailed(long current) =>
        new(412, "common.precondition_failed", null, new Dictionary<string, string[]>(), ETagOf(current));

    private static ApiError NotFound() => FakeAccountClient.Error(404, "common.not_found");

    private static ProductSummaryDto Summary(ProductDto p) => new(p.Id, p.Name, p.Brand, p.CategoryKey, p.DefaultUnit);

    /// <summary>When set, searches return pages of this size; the cursor is the index of the first item of the next page.</summary>
    public int? PageSize { get; set; }

    public Task<ApiResult<CursorPage<ProductSummaryDto>>> SearchProductsAsync(
        Guid householdId, string? query, string? cursor, int? limit, CancellationToken cancellationToken) =>
        Task.FromResult(Respond("search:" + query, () =>
        {
            var found = Products
                .Where(p => string.IsNullOrWhiteSpace(query) || p.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
                .Select(Summary)
                .ToList();
            var skip = cursor is null ? 0 : int.Parse(cursor, System.Globalization.CultureInfo.InvariantCulture);
            var take = PageSize ?? found.Count;
            var next = skip + take < found.Count ? (skip + take).ToString(System.Globalization.CultureInfo.InvariantCulture) : null;
            return ApiResult<CursorPage<ProductSummaryDto>>.Success(new([.. found.Skip(skip).Take(take)], next));
        }));

    public Task<ApiResult<ProductDto>> GetProductAsync(Guid householdId, Guid productId, CancellationToken cancellationToken) =>
        Task.FromResult(Respond("get:" + productId, () => Products.FirstOrDefault(p => p.Id == productId) is { } found
            ? ApiResult<ProductDto>.Success(found, ETagOf(found.Version))
            : ApiResult<ProductDto>.Failure(NotFound())));

    public Task<ApiResult<ProductDto>> FindByBarcodeAsync(Guid householdId, string barcode, CancellationToken cancellationToken) =>
        Task.FromResult(Respond("barcode:" + barcode, () => Products.FirstOrDefault(p => p.Barcodes.Contains(barcode)) is { } found
            ? ApiResult<ProductDto>.Success(found, ETagOf(found.Version))
            : ApiResult<ProductDto>.Failure(NotFound())));

    public Task<ApiResult<ProductDto>> CreateProductAsync(Guid householdId, CreateProductRequest request, CancellationToken cancellationToken) =>
        Task.FromResult(Respond("create-product", () =>
        {
            ArgumentNullException.ThrowIfNull(request);
            var product = new ProductDto(
                request.Id ?? Guid.CreateVersion7(), request.Name, request.Brand, request.CategoryKey, request.DefaultUnit,
                request.DefaultStorageLocationId, request.SectionKey, request.MinimumStock, request.Barcodes ?? [],
                request.FoodFactsBarcode is not null, 1);
            Products.Add(product);
            return ApiResult<ProductDto>.Success(product, ETagOf(1));
        }));

    public Task<ApiResult<ProductDto>> UpdateProductAsync(
        Guid householdId, Guid productId, UpdateProductRequest request, string etag, CancellationToken cancellationToken) =>
        Task.FromResult(Respond("update-product:" + productId, () =>
        {
            ArgumentNullException.ThrowIfNull(request);
            var index = Products.FindIndex(p => p.Id == productId);
            if (index < 0)
            {
                return ApiResult<ProductDto>.Failure(NotFound());
            }

            var current = Products[index];
            if (etag != ETagOf(current.Version))
            {
                return ApiResult<ProductDto>.Failure(PreconditionFailed(current.Version));
            }

            var updated = current with
            {
                Name = request.Name, Brand = request.Brand, CategoryKey = request.CategoryKey, DefaultUnit = request.DefaultUnit,
                DefaultStorageLocationId = request.DefaultStorageLocationId, SectionKey = request.SectionKey,
                MinimumStock = request.MinimumStock, Barcodes = request.Barcodes ?? [], Version = current.Version + 1,
            };
            Products[index] = updated;
            return ApiResult<ProductDto>.Success(updated, ETagOf(updated.Version));
        }));

    public Task<ApiResult> ArchiveProductAsync(Guid householdId, Guid productId, string etag, CancellationToken cancellationToken) =>
        Task.FromResult(RespondEmpty("archive-product:" + productId, () =>
        {
            var current = Products.FirstOrDefault(p => p.Id == productId);
            if (current is null)
            {
                return ApiResult.Failure(NotFound());
            }

            if (etag != ETagOf(current.Version))
            {
                return ApiResult.Failure(PreconditionFailed(current.Version));
            }

            Products.Remove(current);
            return ApiResult.Success();
        }));

    public Task<ApiResult<IReadOnlyList<StorageLocationDto>>> ListStorageLocationsAsync(Guid householdId, CancellationToken cancellationToken) =>
        Task.FromResult(Respond("list-locations", () =>
            ApiResult<IReadOnlyList<StorageLocationDto>>.Success([.. Locations.OrderBy(l => l.SortOrder)])));

    public Task<ApiResult<StorageLocationDto>> CreateStorageLocationAsync(
        Guid householdId, StorageLocationRequest request, CancellationToken cancellationToken) =>
        Task.FromResult(Respond("create-location", () =>
        {
            ArgumentNullException.ThrowIfNull(request);
            var location = new StorageLocationDto(
                request.Id ?? Guid.CreateVersion7(), request.Name, request.Type, request.SortOrder ?? Locations.Count, 1);
            Locations.Add(location);
            return ApiResult<StorageLocationDto>.Success(location, ETagOf(1));
        }));

    public Task<ApiResult<StorageLocationDto>> UpdateStorageLocationAsync(
        Guid householdId, Guid locationId, StorageLocationRequest request, string etag, CancellationToken cancellationToken) =>
        Task.FromResult(Respond("update-location:" + locationId, () =>
        {
            ArgumentNullException.ThrowIfNull(request);
            var index = Locations.FindIndex(l => l.Id == locationId);
            if (index < 0)
            {
                return ApiResult<StorageLocationDto>.Failure(NotFound());
            }

            var current = Locations[index];
            if (etag != ETagOf(current.Version))
            {
                return ApiResult<StorageLocationDto>.Failure(PreconditionFailed(current.Version));
            }

            var updated = current with
            {
                Name = request.Name, Type = request.Type, SortOrder = request.SortOrder ?? current.SortOrder, Version = current.Version + 1,
            };
            Locations[index] = updated;
            return ApiResult<StorageLocationDto>.Success(updated, ETagOf(updated.Version));
        }));

    public Task<ApiResult> DeleteStorageLocationAsync(Guid householdId, Guid locationId, string etag, CancellationToken cancellationToken) =>
        Task.FromResult(RespondEmpty("delete-location:" + locationId, () =>
        {
            var current = Locations.FirstOrDefault(l => l.Id == locationId);
            if (current is null)
            {
                return ApiResult.Failure(NotFound());
            }

            if (etag != ETagOf(current.Version))
            {
                return ApiResult.Failure(PreconditionFailed(current.Version));
            }

            Locations.Remove(current);
            return ApiResult.Success();
        }));

    public Task<ApiResult<IReadOnlyList<CategoryDto>>> ListCategoriesAsync(Guid householdId, CancellationToken cancellationToken) =>
        Task.FromResult(Respond("list-categories", () => ApiResult<IReadOnlyList<CategoryDto>>.Success([.. Categories])));
}
