namespace FoodAssistant.Application.Accounts;

/// <summary>
/// Input rules for new accounts that the identity system does not cover. Password length and
/// allowed user-name characters are checked by Identity itself (ADR-0003).
/// </summary>
public static class AccountValidation
{
    public const int UserNameMaxLength = 64;
    public const int DisplayNameMaxLength = 100;
    public const int PasswordMinLength = 10;

    public static void Validate(string? userName, string? password, string? displayName, IDictionary<string, string[]> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);

        if (string.IsNullOrWhiteSpace(userName))
        {
            errors["userName"] = ["required"];
        }
        else if (userName.Length > UserNameMaxLength)
        {
            errors["userName"] = ["too_long"];
        }

        if (string.IsNullOrEmpty(password))
        {
            errors["password"] = ["required"];
        }
        else if (password.Length < PasswordMinLength)
        {
            // Same code Identity reports, so the UI sees one error whichever check catches it.
            errors["password"] = ["password_too_short"];
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            errors["displayName"] = ["required"];
        }
        else if (displayName.Trim().Length > DisplayNameMaxLength)
        {
            errors["displayName"] = ["too_long"];
        }
    }
}
