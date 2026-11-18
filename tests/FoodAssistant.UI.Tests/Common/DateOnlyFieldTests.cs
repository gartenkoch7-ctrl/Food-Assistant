using Bunit;
using FoodAssistant.UI.Common;
using FoodAssistant.UI.Tests.Support;
using Microsoft.AspNetCore.Components;

namespace FoodAssistant.UI.Tests.Common;

public sealed class DateOnlyFieldTests : UiTestContext
{
    private IRenderedComponent<DateOnlyField> RenderField(DateOnly? value, Action<DateOnly?>? changed = null) =>
        Render<DateOnlyField>(p => p
            .Add(c => c.Label, "Mindestens haltbar bis")
            .Add(c => c.Value, value)
            .Add(c => c.ValueChanged, EventCallback.Factory.Create<DateOnly?>(this, v => changed?.Invoke(v))));

    [Fact]
    public void ShowsTheLabelAndTheDate()
    {
        var cut = RenderField(new DateOnly(2026, 12, 24));

        Assert.Contains("Mindestens haltbar bis", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("24.12.2026", cut.Find("input").GetAttribute("value"), StringComparison.Ordinal);
    }

    [Fact]
    public void WithoutADate_OffersNoClearButton() => Assert.Empty(RenderField(null).FindAll("button[aria-label='Datum entfernen']"));

    [Fact]
    public void ClearButton_HasAnAccessibleName_AndReportsNull()
    {
        DateOnly? received = new DateOnly(2026, 1, 1);
        var cut = RenderField(received, v => received = v);

        var clear = cut.Find("button[aria-label='Datum entfernen']");
        clear.Click();

        Assert.Null(received);
    }
}
