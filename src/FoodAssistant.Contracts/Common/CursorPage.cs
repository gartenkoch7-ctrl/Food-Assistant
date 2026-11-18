namespace FoodAssistant.Contracts.Common;

/// <summary>
/// One page of a list (ADR-0004: cursor pagination, no offsets). <paramref name="NextCursor"/> is
/// opaque; pass it unchanged as <c>?cursor=</c> to get the next page. <c>null</c> means last page.
/// </summary>
public sealed record CursorPage<T>(IReadOnlyList<T> Items, string? NextCursor);
