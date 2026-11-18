using System.Buffers.Text;
using System.Text;
using FoodAssistant.Application.Catalog;

namespace FoodAssistant.Application.Tests.Catalog;

public sealed class ProductCursorTests
{
    [Fact]
    public void Encode_ThenDecode_RoundTrips()
    {
        var cursor = new ProductCursor("Milk", Guid.CreateVersion7());

        Assert.True(ProductCursor.TryDecode(cursor.Encode(), out var decoded));
        Assert.Equal(cursor, decoded);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not base64 !")]
    public void TryDecode_Garbage_IsRejected(string? text) => Assert.False(ProductCursor.TryDecode(text, out _));

    [Fact]
    public void TryDecode_JsonWithoutFields_IsRejected() =>
        Assert.False(ProductCursor.TryDecode(Base64Url.EncodeToString(Encoding.UTF8.GetBytes("{}")), out _));

    [Fact]
    public void TryDecode_TooLong_IsRejected() =>
        Assert.False(ProductCursor.TryDecode(new string('A', 2000), out _));
}
