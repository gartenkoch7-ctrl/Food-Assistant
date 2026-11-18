using FoodAssistant.UI.Scan;

namespace FoodAssistant.UI.Tests.Scan;

public sealed class ScanCodeParserTests
{
    private static readonly Uri AppBase = new("https://food.example.org/");
    private static readonly Guid Batch = Guid.Parse("0197a0c0-0000-7000-8000-000000000042");

    [Theory]
    [InlineData("12345670")]
    [InlineData("036000291452")]
    [InlineData("4006381333931")]
    [InlineData("04006381333931")]
    [InlineData("  4006381333931 \n")]
    public void Digits_OfValidLength_AreABarcode(string raw)
    {
        var code = ScanCodeParser.Parse(raw, AppBase);

        Assert.Equal(ScanCodeKind.Barcode, code.Kind);
        Assert.Equal(raw.Trim(), code.Text);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("1234567")]
    [InlineData("12345678901")]
    [InlineData("123456789012345")]
    [InlineData("40063813339a1")]
    [InlineData("hello")]
    public void OtherText_IsUnknown(string? raw) =>
        Assert.Equal(ScanCodeKind.Unknown, ScanCodeParser.Parse(raw, AppBase).Kind);

    [Theory]
    [InlineData("https://food.example.org/inventory/batches/0197a0c0-0000-7000-8000-000000000042")]
    [InlineData("https://food.example.org/inventory/batches/0197a0c0-0000-7000-8000-000000000042/")]
    [InlineData("http://food.example.org/inventory/batches/0197a0c0-0000-7000-8000-000000000042?x=1")]
    public void LabelOfThisServer_IsABatchLink(string raw)
    {
        var code = ScanCodeParser.Parse(raw, AppBase);

        Assert.Equal((ScanCodeKind.BatchLink, (Guid?)Batch), (code.Kind, code.BatchId));
    }

    [Theory]
    [InlineData("https://evil.example.com/inventory/batches/0197a0c0-0000-7000-8000-000000000042")]
    [InlineData("https://food.example.org/inventory/batches/not-a-guid")]
    [InlineData("https://food.example.org/inventory/products/0197a0c0-0000-7000-8000-000000000042")]
    [InlineData("javascript:alert(1)")]
    [InlineData("/inventory/batches/0197a0c0-0000-7000-8000-000000000042")]
    public void ForeignOrMalformedLinks_AreUnknown(string raw) =>
        Assert.Equal(ScanCodeKind.Unknown, ScanCodeParser.Parse(raw, AppBase).Kind);
}
