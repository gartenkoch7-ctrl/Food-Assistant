using FoodAssistant.UI.Catalog;
using FoodAssistant.UI.Tests.Support;

namespace FoodAssistant.UI.Tests.Catalog;

/// <summary>The UI's section list equals the key file, which the server tests keep equal to the domain list.</summary>
public sealed class SectionKeysTests
{
    [Fact]
    public void All_EqualsTheSectionKeyFile() =>
        Assert.Equal(
            File.ReadAllLines(RepoFiles.Path("tests/FoodAssistant.UI.Tests/Catalog/section-keys.txt")).Where(l => l.Length > 0),
            SectionKeys.All);
}
