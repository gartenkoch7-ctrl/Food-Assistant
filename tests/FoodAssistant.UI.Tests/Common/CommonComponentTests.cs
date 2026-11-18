using Bunit;
using FoodAssistant.UI.Common;
using FoodAssistant.UI.Tests.Support;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace FoodAssistant.UI.Tests.Common;

public sealed class CommonComponentTests : UiTestContext
{
    [Fact]
    public void ListRow_ShowsIconNameSecondaryAndEndSlot()
    {
        var cut = Render<ListRow>(p => p
            .Add(c => c.Icon, Icons.Material.Rounded.Kitchen)
            .Add(c => c.Name, "Milch")
            .Add(c => c.Secondary, "1 l · Kühlschrank")
            .Add(c => c.End, (RenderFragment)(b => b.AddMarkupContent(0, "<button>Abhaken</button>"))));

        Assert.Equal("Milch", cut.Find(".fa-list-row-name").TextContent);
        Assert.Equal("1 l · Kühlschrank", cut.Find(".fa-list-row-secondary").TextContent);
        Assert.Equal("Abhaken", cut.Find(".fa-list-row-end button").TextContent);
        Assert.Equal("true", cut.Find(".fa-list-row-icon").GetAttribute("aria-hidden"));
        Assert.NotNull(cut.Find(".fa-list-row-icon svg"));
    }

    [Fact]
    public void ListRow_WithoutOptionalParts_RendersNoEmptyElements()
    {
        var cut = Render<ListRow>(p => p
            .Add(c => c.Icon, Icons.Material.Rounded.Kitchen)
            .Add(c => c.Name, "Reis"));

        Assert.Empty(cut.FindAll(".fa-list-row-secondary"));
        Assert.Empty(cut.FindAll(".fa-list-row-end"));
        Assert.Equal("LI", cut.Find(".fa-list-row").TagName);
    }

    [Fact]
    public void EmptyState_NamesItsRegionAfterTheHeading()
    {
        var cut = Render<EmptyState>(p => p
            .Add(c => c.Icon, Icons.Material.Rounded.Kitchen)
            .Add(c => c.Heading, "Leer")
            .Add(c => c.Text, "Noch nichts da."));

        var section = cut.Find("section");
        var heading = cut.Find("h2");
        Assert.Equal(heading.Id, section.GetAttribute("aria-labelledby"));
        Assert.Equal("Leer", heading.TextContent);
        Assert.Equal("Noch nichts da.", cut.Find("p").TextContent);
        Assert.Empty(cut.FindAll(".fa-empty-actions"));
    }

    [Fact]
    public void OptionGroup_UsesAFieldsetWithLegend_AndOneRadioGroup()
    {
        var cut = Render<OptionGroup<int>>(p => p
            .Add(c => c.Legend, "Zahl")
            .Add(c => c.Options, [new ChoiceOption<int>(1, "Eins"), new ChoiceOption<int>(2, "Zwei")])
            .Add(c => c.Value, 2));

        Assert.Equal("Zahl", cut.Find("fieldset > legend").TextContent);
        var radios = cut.FindAll("input[type=radio]");
        Assert.Equal(2, radios.Count);
        Assert.Single(radios.Select(r => r.GetAttribute("name")).Distinct());
        Assert.False(radios[0].HasAttribute("checked"));
        Assert.True(radios[1].HasAttribute("checked"));
    }

    [Fact]
    public void OptionGroup_ReportsTheChosenValue()
    {
        var chosen = 0;
        var cut = Render<OptionGroup<int>>(p => p
            .Add(c => c.Legend, "Zahl")
            .Add(c => c.Options, [new ChoiceOption<int>(1, "Eins"), new ChoiceOption<int>(2, "Zwei")])
            .Add(c => c.Value, 1)
            .Add(c => c.ValueChanged, EventCallback.Factory.Create<int>(this, value => chosen = value)));

        cut.FindAll("input[type=radio]")[1].Change(true);

        Assert.Equal(2, chosen);
    }
}
