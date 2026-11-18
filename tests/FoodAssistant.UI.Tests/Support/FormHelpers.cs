using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;

namespace FoodAssistant.UI.Tests.Support;

/// <summary>Finds form controls the way a person does: by their visible label.</summary>
public static class FormHelpers
{
    /// <summary>The input whose label text (without the required asterisk) is <paramref name="label"/>.</summary>
    public static IElement Field<TComponent>(this IRenderedComponent<TComponent> cut, string label)
        where TComponent : IComponent
    {
        ArgumentNullException.ThrowIfNull(cut);
        var labelElement = cut.FindAll("label").First(l => l.TextContent.Trim().TrimEnd('*').Trim() == label);
        return cut.Find("#" + labelElement.GetAttribute("for"));
    }

    /// <summary>All text of the page that screen readers announce as errors: alerts and field error texts.</summary>
    public static string ErrorText<TComponent>(this IRenderedComponent<TComponent> cut)
        where TComponent : IComponent
    {
        ArgumentNullException.ThrowIfNull(cut);
        return string.Join(" | ", cut.FindAll("[role=alert], .mud-input-error").Select(e => e.TextContent.Trim()));
    }
}
