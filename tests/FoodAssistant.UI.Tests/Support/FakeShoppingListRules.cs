using FoodAssistant.Client.Http;
using FoodAssistant.Contracts.Shopping;

namespace FoodAssistant.UI.Tests.Support;

/// <summary>Server rules for list updates and deletes, applied by <see cref="FakeShoppingClient"/> to its in-memory lists.</summary>
internal static class FakeShoppingListRules
{
    public static string ETagOf(long version) => $"\"{version}\"";

    /// <summary>412 on a stale ETag; making a list the default resets the others; the default flag cannot be dropped.</summary>
    public static ApiResult<ShoppingListDto> Update(List<ShoppingListDto> lists, Guid listId, UpdateShoppingListRequest request, string etag)
    {
        ArgumentNullException.ThrowIfNull(request);
        var index = lists.FindIndex(l => l.Id == listId);
        if (index < 0)
        {
            return ApiResult<ShoppingListDto>.Failure(FakeAccountClient.Error(404, ShoppingErrorCodes.ListNotFound));
        }

        var current = lists[index];
        if (etag != ETagOf(current.Version))
        {
            return ApiResult<ShoppingListDto>.Failure(
                new ApiError(412, "common.precondition_failed", null, new Dictionary<string, string[]>(), ETagOf(current.Version)));
        }

        if (request.IsDefault)
        {
            for (var i = 0; i < lists.Count; i++)
            {
                lists[i] = lists[i] with { IsDefault = false };
            }
        }

        var updated = current with
        {
            Name = request.Name, StoreId = request.StoreId, IsDefault = request.IsDefault || current.IsDefault,
            SortOrder = request.SortOrder, Version = current.Version + 1,
        };
        lists[index] = updated;
        return ApiResult<ShoppingListDto>.Success(updated, ETagOf(updated.Version));
    }

    /// <summary>404 for an unknown list, 409 for the default list; otherwise removes the list with its items.</summary>
    public static ApiResult Delete(List<ShoppingListDto> lists, List<ShoppingItemDto> items, Guid listId)
    {
        var current = lists.FirstOrDefault(l => l.Id == listId);
        if (current is null)
        {
            return ApiResult.Failure(FakeAccountClient.Error(404, ShoppingErrorCodes.ListNotFound));
        }

        if (current.IsDefault)
        {
            return ApiResult.Failure(FakeAccountClient.Error(409, ShoppingErrorCodes.DefaultList));
        }

        lists.Remove(current);
        items.RemoveAll(i => i.ListId == listId);
        return ApiResult.Success();
    }
}
