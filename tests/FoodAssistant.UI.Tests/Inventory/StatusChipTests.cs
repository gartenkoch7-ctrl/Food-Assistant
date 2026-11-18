using Bunit;
using FoodAssistant.Contracts.Inventory;
using FoodAssistant.UI.Inventory;
using FoodAssistant.UI.Tests.Support;
using MudBlazor;

namespace FoodAssistant.UI.Tests.Inventory;

public sealed class StatusChipTests : UiTestContext
{
    public static TheoryData<ExpiryStatusDto, string, string, string> Statuses => new()
    {
        { ExpiryStatusDto.Expired, "Abgelaufen", "fa-status-expired", Icons.Material.Rounded.Error },
        { ExpiryStatusDto.Soon, "Läuft bald ab", "fa-status-soon", Icons.Material.Rounded.Schedule },
        { ExpiryStatusDto.Ok, "Haltbar", "fa-status-ok", Icons.Material.Rounded.CheckCircle },
        { ExpiryStatusDto.NoDate, "Kein Datum", "fa-status-none", Icons.Material.Rounded.Help },
    };

    [Theory]
    [MemberData(nameof(Statuses))]
    public void Renders_TextSymbolAndClass_ForEachStatus(ExpiryStatusDto status, string text, string cssClass, string icon)
    {
        var cut = Render<StatusChip>(p => p.Add(c => c.Status, status));

        var chip = cut.Find(".fa-status-chip");
        Assert.Contains(cssClass, chip.ClassList);
        Assert.Equal(text, cut.Find(".fa-status-chip-text").TextContent.Trim());

        // Same inner markup as a MudIcon with the expected symbol: shape, not just colour, tells the status apart.
        var expected = Render<MudIcon>(p => p.Add(c => c.Icon, icon)).Find("svg").InnerHtml;
        Assert.Equal(expected, cut.Find("svg").InnerHtml);
    }

    [Fact]
    public void Symbols_DifferBetweenStatuses()
    {
        var symbols = Enum.GetValues<ExpiryStatusDto>()
            .Select(status => Render<StatusChip>(p => p.Add(c => c.Status, status)).Find("svg").InnerHtml)
            .ToList();

        Assert.Equal(symbols.Count, symbols.Distinct().Count());
    }

    [Fact]
    public void Symbol_IsHiddenFromScreenReaders()
    {
        var cut = Render<StatusChip>(p => p.Add(c => c.Status, ExpiryStatusDto.Soon));

        Assert.Equal("true", cut.Find("svg").GetAttribute("aria-hidden"));
    }

    [Fact]
    public void DetailText_ReplacesVisibleWord_ButStatusStaysForScreenReaders()
    {
        var cut = Render<StatusChip>(p => p
            .Add(c => c.Status, ExpiryStatusDto.Soon)
            .Add(c => c.Text, "morgen"));

        Assert.Equal("Läuft bald ab: ", cut.Find(".fa-visually-hidden").TextContent);
        Assert.Contains("morgen", cut.Find(".fa-status-chip-text").TextContent);
    }

    [Fact]
    public void WithoutDetailText_NoHiddenPrefixIsRendered()
    {
        var cut = Render<StatusChip>(p => p.Add(c => c.Status, ExpiryStatusDto.Expired));

        Assert.Empty(cut.FindAll(".fa-visually-hidden"));
    }

    [Fact]
    public void Text_FollowsTheCulture()
    {
        UseCulture("en");

        var cut = Render<StatusChip>(p => p.Add(c => c.Status, ExpiryStatusDto.Expired));

        Assert.Equal("Expired", cut.Find(".fa-status-chip-text").TextContent.Trim());
    }
}
