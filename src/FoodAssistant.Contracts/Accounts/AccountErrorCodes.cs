namespace FoodAssistant.Contracts.Accounts;

/// <summary>Stable <c>errorCode</c> values of account endpoints (ADR-0004). The UI translates these.</summary>
public static class AccountErrorCodes
{
    /// <summary>Setup was already completed; registration is closed (409).</summary>
    public const string SetupAlreadyDone = "accounts.setup_already_done";

    /// <summary>Request data is invalid; see <c>errors</c> (400).</summary>
    public const string ValidationFailed = "accounts.validation_failed";

    /// <summary>The user name is already in use (409).</summary>
    public const string UserNameTaken = "accounts.username_taken";

    /// <summary>User name or password wrong (401). Deliberately not distinguished.</summary>
    public const string InvalidCredentials = "accounts.invalid_credentials";

    /// <summary>Too many failed attempts; the account is locked for 15 minutes (429).</summary>
    public const string LockedOut = "accounts.locked_out";

    /// <summary>Not signed in (401), e.g. at <c>/connect/authorize</c> before the web login.</summary>
    public const string LoginRequired = "accounts.login_required";

    /// <summary>Only the server admin may do this (403).</summary>
    public const string ServerAdminRequired = "accounts.server_admin_required";

    /// <summary>Antiforgery token missing or invalid on a cookie-authenticated write (400).</summary>
    public const string AntiforgeryInvalid = "accounts.antiforgery_invalid";

    /// <summary>Web sign-in over plain HTTP; the Secure-only cookies need HTTPS (400).</summary>
    public const string HttpsRequired = "accounts.https_required";
}
