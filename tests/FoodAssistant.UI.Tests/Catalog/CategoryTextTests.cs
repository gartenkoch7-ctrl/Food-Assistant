using FoodAssistant.UI.Catalog;
using FoodAssistant.UI.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

namespace FoodAssistant.UI.Tests.Catalog;

/// <summary>
/// Every category and store section key of the fixed lists has a text in German and English. The key
/// files are kept equal to the domain lists by tests in the server test project (it knows the domain).
/// </summary>
public sealed class CategoryTextTests : UiTestContext
{
    private static IReadOnlyList<string> KeysOf(string file) =>
        [.. File.ReadAllLines(RepoFiles.Path("tests/FoodAssistant.UI.Tests/Catalog/" + file)).Where(l => l.Length > 0)];

    public static TheoryData<string> CultureNames => ["de", "en"];

    [Theory]
    [MemberData(nameof(CultureNames))]
    public void EveryCategoryKey_HasAText(string culture)
    {
        UseCulture(culture);
        var texts = Services.GetRequiredService<IStringLocalizer<CategoryText>>();

        foreach (var key in KeysOf("category-keys.txt"))
        {
            Assert.False(texts[key].ResourceNotFound, $"{key} missing for {culture}");
        }
    }

    [Theory]
    [MemberData(nameof(CultureNames))]
    public void EverySectionKey_HasAText(string culture)
    {
        UseCulture(culture);
        var texts = Services.GetRequiredService<IStringLocalizer<SectionText>>();

        foreach (var key in KeysOf("section-keys.txt"))
        {
            Assert.False(texts[key].ResourceNotFound, $"{key} missing for {culture}");
        }
    }

    [Fact]
    public void ThereAre24CategoriesAnd11Sections()
    {
        Assert.Equal(24, KeysOf("category-keys.txt").Count);
        Assert.Equal(11, KeysOf("section-keys.txt").Count);
    }

    [Fact]
    public void Categories_AreTranslatedIntoGermanAndEnglish()
    {
        var texts = Services.GetRequiredService<IStringLocalizer<CategoryText>>();
        Assert.Equal("Milchprodukte", texts["dairy"].Value);

        UseCulture("en");

        Assert.Equal("Dairy", texts["dairy"].Value);
    }

    [Fact]
    public void EveryCategory_HasAnIcon()
    {
        foreach (var key in KeysOf("category-keys.txt"))
        {
            Assert.False(string.IsNullOrWhiteSpace(CategoryIcons.For(key)), key);
        }

        Assert.False(string.IsNullOrWhiteSpace(CategoryIcons.For("unknown")));
    }
}
