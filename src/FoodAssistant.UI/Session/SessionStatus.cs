namespace FoodAssistant.UI.Session;

/// <summary>Where the browser session stands; the shell shows a page, a redirect or a hint accordingly.</summary>
public enum SessionStatus
{
    /// <summary>Not yet asked the server.</summary>
    Loading,

    /// <summary>No account exists yet; the first start has to create one.</summary>
    NeedsSetup,

    /// <summary>Accounts exist, but nobody is signed in.</summary>
    SignedOut,

    /// <summary>Signed in, but a member of no household.</summary>
    NoHousehold,

    /// <summary>Signed in with an active household.</summary>
    Ready,
}
