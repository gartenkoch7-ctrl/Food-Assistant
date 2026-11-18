namespace FoodAssistant.UI.Accounts;

/// <summary>Writes text to the system clipboard (invitation link). Needs a secure context and a user gesture.</summary>
public interface IClipboard
{
    /// <returns><see langword="true"/> when the text is on the clipboard; <see langword="false"/> when the browser refused or is not available.</returns>
    Task<bool> WriteTextAsync(string text, CancellationToken cancellationToken = default);
}
