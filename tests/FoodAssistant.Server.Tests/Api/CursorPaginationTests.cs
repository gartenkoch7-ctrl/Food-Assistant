using FoodAssistant.Server.Api.Pagination;

namespace FoodAssistant.Server.Tests.Api;

public sealed class CursorPaginationTests
{
    private sealed record Position(DateOnly ExpiresOn, Guid Id);

    [Fact]
    public void EncodeThenDecode_RoundTripsPosition()
    {
        var position = new Position(new DateOnly(2026, 12, 24), Guid.CreateVersion7());

        var cursor = CursorPagination.Encode(position);

        Assert.True(CursorPagination.TryDecode<Position>(cursor, out var decoded));
        Assert.Equal(position, decoded);
        Assert.Matches("^[A-Za-z0-9_-]+$", cursor);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("%%%")]
    [InlineData("bm90IGpzb24")]
    [InlineData("e30")]
    public void TryDecode_RejectsMalformedCursor(string? cursor)
    {
        Assert.False(CursorPagination.TryDecode<Position>(cursor, out _));
    }

    [Fact]
    public void TryDecode_RejectsOverlongCursor()
    {
        Assert.False(CursorPagination.TryDecode<Position>(new string('A', 2000), out _));
    }

    [Theory]
    [InlineData(null, CursorPagination.DefaultLimit)]
    [InlineData(0, CursorPagination.DefaultLimit)]
    [InlineData(10, 10)]
    [InlineData(10_000, CursorPagination.MaxLimit)]
    public void NormalizeLimit_AppliesDefaultAndMaximum(int? requested, int expected)
    {
        Assert.Equal(expected, CursorPagination.NormalizeLimit(requested));
    }

    [Fact]
    public void ToPage_WithExtraItem_ReturnsLimitItemsAndCursorOfLastItem()
    {
        int[] items = [1, 2, 3];

        var page = CursorPagination.ToPage(items, limit: 2, positionOf: item => item);

        Assert.Equal([1, 2], page.Items);
        Assert.True(CursorPagination.TryDecode<int>(page.NextCursor, out var last));
        Assert.Equal(2, last);
    }

    [Fact]
    public void ToPage_WithoutExtraItem_IsLastPage()
    {
        int[] items = [1, 2];

        var page = CursorPagination.ToPage(items, limit: 2, positionOf: item => item);

        Assert.Equal(2, page.Items.Count);
        Assert.Null(page.NextCursor);
    }
}
