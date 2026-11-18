using Bunit;
using FoodAssistant.UI.Accounts;
using FoodAssistant.UI.Tests.Support;

namespace FoodAssistant.UI.Tests.Accounts;

public sealed class QrCodeSvgTests : UiTestContext
{
    private IRenderedComponent<QrCodeSvg> RenderCode(string text) =>
        Render<QrCodeSvg>(p => p.Add(c => c.Text, text).Add(c => c.Label, "QR-Code für den Link"));

    [Fact]
    public void Renders_AnSvg_WithATextAlternative()
    {
        var cut = RenderCode("https://food.example/invite#token=abc");

        var container = cut.Find("div.fa-qr");
        Assert.Equal("img", container.GetAttribute("role"));
        Assert.Equal("QR-Code für den Link", container.GetAttribute("aria-label"));
        Assert.NotNull(cut.Find("svg"));
    }

    [Fact]
    public void TheSvg_NeverContainsTheText()
    {
        var cut = RenderCode("https://food.example/invite#token=secret-token");

        Assert.DoesNotContain("secret-token", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void DifferentTexts_GiveDifferentCodes()
    {
        var first = RenderCode("https://food.example/invite#token=aaa").Find("svg").InnerHtml;
        var second = RenderCode("https://food.example/invite#token=bbb").Find("svg").InnerHtml;

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void ChangedText_RedrawsTheCode()
    {
        var cut = RenderCode("https://food.example/invite#token=aaa");
        var before = cut.Find("svg").InnerHtml;

        cut.Render(p => p.Add(c => c.Text, "https://food.example/invite#token=bbb").Add(c => c.Label, "x"));

        Assert.NotEqual(before, cut.Find("svg").InnerHtml);
    }
}
