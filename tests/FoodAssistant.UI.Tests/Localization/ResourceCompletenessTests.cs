using System.Text.RegularExpressions;
using System.Xml.Linq;
using FoodAssistant.UI.Common;
using FoodAssistant.UI.Tests.Support;

namespace FoodAssistant.UI.Tests.Localization;

/// <summary>Every UI text exists in German (neutral) and English (ADR-0008), and every text the code asks for exists.</summary>
public sealed partial class ResourceCompletenessTests
{
    private static readonly string[] Languages = [.. SupportedCultures.All.Where(l => l != SupportedCultures.German)];

    public static TheoryData<string> TextGroups
    {
        get
        {
            var data = new TheoryData<string>();
            foreach (var neutral in RepoFiles.UiFiles("*.resx").Where(IsNeutral).OrderBy(f => f, StringComparer.Ordinal))
            {
                data.Add(System.IO.Path.GetRelativePath(RepoFiles.UiPath(string.Empty), neutral).Replace('\\', '/'));
            }

            return data;
        }
    }

    [GeneratedRegex("\\bT\\[\"([A-Za-z0-9_]+)\"")]
    private static partial Regex LocalizerKey();

    private static bool IsNeutral(string file) => !System.IO.Path.GetFileNameWithoutExtension(file).Contains('.');

    private static Dictionary<string, string> Entries(string file) =>
        XDocument.Load(file).Root!.Elements("data")
            .ToDictionary(d => (string)d.Attribute("name")!, d => (string?)d.Element("value") ?? string.Empty);

    private static string Translation(string neutralRelativePath, string language) =>
        RepoFiles.UiPath(neutralRelativePath[..^".resx".Length] + "." + language + ".resx");

    [Fact]
    public void ThereAreTextGroups() => Assert.NotEmpty(TextGroups);

    [Theory]
    [MemberData(nameof(TextGroups))]
    public void EveryLanguage_HasExactlyTheKeysOfTheNeutralFile(string neutralRelativePath)
    {
        var neutralKeys = Entries(RepoFiles.UiPath(neutralRelativePath)).Keys.Order(StringComparer.Ordinal).ToList();

        foreach (var language in Languages)
        {
            var file = Translation(neutralRelativePath, language);
            Assert.True(File.Exists(file), $"Missing translation file {file}");
            Assert.Equal(neutralKeys, Entries(file).Keys.Order(StringComparer.Ordinal).ToList());
        }
    }

    [Theory]
    [MemberData(nameof(TextGroups))]
    public void NoTextIsEmpty(string neutralRelativePath)
    {
        var files = Languages.Select(l => Translation(neutralRelativePath, l)).Prepend(RepoFiles.UiPath(neutralRelativePath));

        foreach (var file in files)
        {
            foreach (var (key, value) in Entries(file))
            {
                Assert.False(string.IsNullOrWhiteSpace(value), $"{key} is empty in {file}");
            }
        }
    }

    [Fact]
    public void EveryKeyUsedInARazorFile_ExistsInAResourceFile()
    {
        var allKeys = RepoFiles.UiFiles("*.resx").Where(IsNeutral).SelectMany(file => Entries(file).Keys).ToHashSet(StringComparer.Ordinal);
        var serverPages = Directory.EnumerateFiles(RepoFiles.Path("src/FoodAssistant.Server/Components"), "*.razor", SearchOption.AllDirectories);

        var missing = RepoFiles.UiFiles("*.razor").Concat(serverPages)
            .SelectMany(file => LocalizerKey().Matches(File.ReadAllText(file)).Select(m => (File: file, Key: m.Groups[1].Value)))
            .Where(use => !allKeys.Contains(use.Key))
            .Select(use => $"{System.IO.Path.GetFileName(use.File)}: {use.Key}")
            .ToList();

        Assert.Empty(missing);
    }
}
