namespace FoodAssistant.UI.Accounts;

/// <summary>
/// Keeps the invitation token in memory while a person signs in on the login page and comes back
/// (<c>/invite</c> → <c>/login</c> → <c>/invite</c>). Memory only: the token never goes to browser
/// storage, a URL or a log, and a reload of the page forgets it (the invitation link works again).
/// </summary>
public sealed class InvitationDraft
{
    public string? Token { get; set; }
}
