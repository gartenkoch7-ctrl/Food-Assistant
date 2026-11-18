namespace FoodAssistant.UI.Common;

/// <summary>Shows a short message with an "Undo" action after a change (checking off, removing); the action stays available for 10 seconds.</summary>
public interface IUndoNotifier
{
    void Show(string message, Func<Task> undo);
}
