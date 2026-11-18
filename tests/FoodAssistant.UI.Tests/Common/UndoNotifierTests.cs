using Bunit;
using FoodAssistant.UI.Common;
using FoodAssistant.UI.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;

namespace FoodAssistant.UI.Tests.Common;

public sealed class UndoNotifierTests : UiTestContext
{
    [Fact]
    public void Show_ShowsTheMessageWithAnUndoButton_ThatRunsTheCallback()
    {
        var undone = false;
        var provider = Render<MudSnackbarProvider>();

        provider.InvokeAsync(() => Services.GetRequiredService<IUndoNotifier>().Show("Milch entfernt", () =>
        {
            undone = true;
            return Task.CompletedTask;
        }));

        provider.WaitForAssertion(() => Assert.Contains("Milch entfernt", provider.Markup, StringComparison.Ordinal));
        var undo = provider.Find("button.mud-snackbar-action-button, .mud-snackbar-action button");
        Assert.Contains("Rückgängig", undo.TextContent);
        undo.Click();
        Assert.True(undone);
    }

    [Fact]
    public void Show_QueuesOneSnackbar()
    {
        Services.GetRequiredService<IUndoNotifier>().Show("Milch entfernt", () => Task.CompletedTask);

        var shown = Assert.Single(Services.GetRequiredService<ISnackbar>().ShownSnackbars);
        Assert.Equal("Milch entfernt", shown.Message);
    }
}
