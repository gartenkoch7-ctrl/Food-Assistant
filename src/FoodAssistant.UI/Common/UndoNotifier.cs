using Microsoft.Extensions.Localization;
using MudBlazor;

namespace FoodAssistant.UI.Common;

internal sealed class UndoNotifier(ISnackbar snackbar, IStringLocalizer<CommonText> text) : IUndoNotifier
{
    /// <summary>How long the Undo action stays available (ms).</summary>
    public const int VisibleMilliseconds = 10_000;

    public void Show(string message, Func<Task> undo)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(undo);

        snackbar.Add(message, Severity.Normal, options =>
        {
            options.Action = text["Undo"];
            options.ActionColor = Color.Primary;
            options.OnClick = _ => undo();
            options.VisibleStateDuration = VisibleMilliseconds;
            options.RequireInteraction = false;
        });
    }
}
