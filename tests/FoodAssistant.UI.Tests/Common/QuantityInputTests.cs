using Bunit;
using FoodAssistant.Contracts.Common;
using FoodAssistant.UI.Common;
using FoodAssistant.UI.Tests.Support;
using Microsoft.AspNetCore.Components;

namespace FoodAssistant.UI.Tests.Common;

public sealed class QuantityInputTests : UiTestContext
{
    private IRenderedComponent<QuantityInput> RenderInput(QuantityDto? value, Action<QuantityDto?>? changed = null) =>
        Render<QuantityInput>(p => p
            .Add(c => c.Label, "Menge")
            .Add(c => c.Value, value)
            .Add(c => c.ValueChanged, EventCallback.Factory.Create<QuantityDto?>(this, v => changed?.Invoke(v))));

    [Fact]
    public void ShowsTheLabelledGroupWithNumberAndUnit()
    {
        var cut = RenderInput(new QuantityDto(1.5m, UnitCodes.Kilogram));

        Assert.Equal("Menge", cut.Find("[role=group]").GetAttribute("aria-label"));
        Assert.Equal("1.5", cut.Find("input.mud-input-slot").GetAttribute("value"));
        Assert.Contains("kg", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("Einheit", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Typing_ANumber_ReportsAQuantityWithTheDefaultUnit()
    {
        QuantityDto? received = null;
        var cut = RenderInput(null, v => received = v);

        cut.Find("input.mud-input-slot").Change("2.5");

        Assert.Equal(new QuantityDto(2.5m, UnitCodes.Piece), received);
    }

    [Fact]
    public void ClearingTheNumber_ReportsNull()
    {
        QuantityDto? received = new QuantityDto(1, UnitCodes.Litre);
        var cut = RenderInput(received, v => received = v);

        cut.Find("input.mud-input-slot").Change(string.Empty);

        Assert.Null(received);
    }

    [Fact]
    public void English_ShowsUnitNamesInEnglish()
    {
        UseCulture("en");

        var cut = RenderInput(new QuantityDto(2, UnitCodes.Piece));

        Assert.Contains("piece", cut.Markup, StringComparison.Ordinal);
    }
}
