using FoodAssistant.Domain.Catalog;
using FoodAssistant.Domain.Shopping;

namespace FoodAssistant.Server.Tests.Support;

/// <summary>
/// The UI tests cannot see the domain, so they read key lists from files. These tests keep the
/// files equal to the fixed lists in the domain.
/// </summary>
public sealed class CategoryKeysFileTests
{
    private static IReadOnlyList<string> KeysOf(string file)
    {
        // Tests run from tests/<project>/bin/...; the repository root holds the solution file.
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "FoodAssistant.slnx")))
        {
            root = root.Parent;
        }

        Assert.NotNull(root);
        var path = Path.Combine(root.FullName, "tests", "FoodAssistant.UI.Tests", "Catalog", file);
        return [.. File.ReadAllLines(path).Where(l => l.Length > 0)];
    }

    [Fact]
    public void CategoryKeysFile_EqualsTheDomainList() =>
        Assert.Equal(ProductCategories.All, KeysOf("category-keys.txt"));

    [Fact]
    public void SectionKeysFile_EqualsTheDomainList() =>
        Assert.Equal(StoreSections.All, KeysOf("section-keys.txt"));
}
