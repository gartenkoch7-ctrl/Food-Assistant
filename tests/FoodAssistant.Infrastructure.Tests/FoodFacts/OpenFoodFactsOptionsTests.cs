using FoodAssistant.Infrastructure.FoodFacts;

namespace FoodAssistant.Infrastructure.Tests.FoodFacts;

public sealed class OpenFoodFactsOptionsTests
{
    [Fact]
    public void Defaults_AreValid_AndUseProjectContact()
    {
        var options = new OpenFoodFactsOptions();

        Assert.Null(options.Validate());
        Assert.Equal("https://github.com/gartenkoch7-ctrl/Food-Assistant", options.Contact);
        Assert.Equal(60, options.RequestsPerMinute);
        Assert.Equal(5, options.TimeoutSeconds);
    }

    [Theory]
    [InlineData("http://world.openfoodfacts.org/")]
    [InlineData("world.openfoodfacts.org")]
    [InlineData("")]
    public void BaseUrl_MustBeAbsoluteHttps(string url) =>
        Assert.NotNull(new OpenFoodFactsOptions { BaseUrl = url }.Validate());

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("a (b)")]
    [InlineData("a\r\nX-Injected: 1")]
    public void Contact_MustBeSingleLineWithoutParentheses(string contact) =>
        Assert.NotNull(new OpenFoodFactsOptions { Contact = contact }.Validate());

    [Fact]
    public void Limits_MustBePositive()
    {
        Assert.NotNull(new OpenFoodFactsOptions { TimeoutSeconds = 0 }.Validate());
        Assert.NotNull(new OpenFoodFactsOptions { RequestsPerMinute = 0 }.Validate());
    }
}
