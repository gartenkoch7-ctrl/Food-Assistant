using FoodAssistant.Client.FoodFacts;
using FoodAssistant.Client.Http;
using FoodAssistant.Contracts.FoodFacts;

namespace FoodAssistant.UI.Tests.Support;

/// <summary>
/// Open Food Facts lookup over a dictionary: known barcodes answer their suggestion, unknown ones
/// 404 like the server, and <see cref="FailWith"/> answers every call with an error (503 ...).
/// </summary>
public sealed class FakeFoodFactsClient : IFoodFactsClient
{
    public Dictionary<string, FoodFactsProductDto> Suggestions { get; } = [];

    public List<string> Calls { get; } = [];

    public ApiError? FailWith { get; set; }

    public Task<ApiResult<FoodFactsProductDto>> LookupAsync(Guid householdId, string barcode, CancellationToken cancellationToken)
    {
        Calls.Add(barcode);
        if (FailWith is not null)
        {
            return Task.FromResult(ApiResult<FoodFactsProductDto>.Failure(FailWith));
        }

        return Task.FromResult(Suggestions.TryGetValue(barcode, out var found)
            ? ApiResult<FoodFactsProductDto>.Success(found)
            : ApiResult<FoodFactsProductDto>.Failure(FakeAccountClient.Error(404, FoodFactsErrorCodes.NotFound)));
    }
}
