using FoodAssistant.Application.FoodFacts;

namespace FoodAssistant.Application.Tests.FoodFacts;

public sealed class FoodFactsSuggestionTests
{
    private static FoodFactsProduct Product(decimal? quantity = 1000, string? unit = "ml") => new(
        "4000000000017", "Testmilch", "Testmilch DE", "Test milk EN", "Testmarke", "1 l", quantity, unit, ["en:milks"]);

    [Theory]
    [InlineData("de", "Testmilch DE")]
    [InlineData("de-AT", "Testmilch DE")]
    [InlineData("en", "Test milk EN")]
    [InlineData("fr", "Testmilch")]
    [InlineData("", "Testmilch")]
    public void Name_FollowsRequestLanguage(string culture, string expected)
    {
        var dto = FoodFactsSuggestion.ToDto(Product(), culture, new FixedResolver("dairy"), stale: false);

        Assert.Equal(expected, dto.Name);
    }

    [Fact]
    public void Name_FallsBackToGenericWhenLanguageMissing()
    {
        var product = Product() with { NameDe = null };

        Assert.Equal("Testmilch", FoodFactsSuggestion.ToDto(product, "de", new FixedResolver(null), false).Name);
    }

    [Theory]
    [InlineData("g")]
    [InlineData("kg")]
    [InlineData("ml")]
    [InlineData("L")]
    public void PackageQuantity_KeptForSupportedUnits(string unit)
    {
        var dto = FoodFactsSuggestion.ToDto(Product(500, unit), "de", new FixedResolver(null), false);

        Assert.Equal(500, dto.PackageQuantity?.Value);
        Assert.Equal(unit.ToLowerInvariant(), dto.PackageQuantity?.Unit);
    }

    [Theory]
    [InlineData("cl")]
    [InlineData("oz")]
    [InlineData(null)]
    public void PackageQuantity_NullForOtherUnits(string? unit)
    {
        Assert.Null(FoodFactsSuggestion.ToDto(Product(33, unit), "de", new FixedResolver(null), false).PackageQuantity);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(null)]
    public void PackageQuantity_NullWhenNotPositive(int? quantity)
    {
        Assert.Null(FoodFactsSuggestion.ToDto(Product(quantity, "g"), "de", new FixedResolver(null), false).PackageQuantity);
    }

    [Fact]
    public void CategoryAndStale_AreTakenOver()
    {
        var dto = FoodFactsSuggestion.ToDto(Product(), "de", new FixedResolver("dairy"), stale: true);

        Assert.Equal("dairy", dto.SuggestedCategoryKey);
        Assert.True(dto.Stale);
        Assert.Equal("Testmarke", dto.Brand);
    }

    private sealed class FixedResolver(string? key) : IFoodFactsCategoryResolver
    {
        public string? CategoryOf(IReadOnlyList<string> categoryTags) => key;
    }
}
