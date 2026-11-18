using FoodAssistant.Client.Http;
using FoodAssistant.Contracts.Households;
using Microsoft.Extensions.Localization;

namespace FoodAssistant.UI.Accounts;

/// <summary>Maps contract values and server error codes onto keys of <see cref="AccountsText"/>. Pure logic, no state.</summary>
internal static class AccountsKeys
{
    public static string Role(HouseholdRole role) => "Role." + RoleName(role);

    public static string RoleHint(HouseholdRole role) => "RoleHint." + RoleName(role);

    public static string Status(InvitationStatus status) => "InvitationStatus." + status switch
    {
        InvitationStatus.Pending => "pending",
        InvitationStatus.Accepted => "accepted",
        InvitationStatus.Revoked => "revoked",
        _ => "expired",
    };

    /// <summary>
    /// The text of the first error code the server reported for <paramref name="field"/> (JSON name),
    /// or <see langword="null"/> when that field has no error. Unknown codes fall back to a generic text.
    /// </summary>
    public static string? FieldError(IStringLocalizer text, ApiError? error, string field)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (error is null || !error.FieldErrors.TryGetValue(field, out var codes) || codes.Length == 0)
        {
            return null;
        }

        var specific = text["FieldError." + codes[0]];
        return specific.ResourceNotFound ? text["FieldError.invalid"].Value : specific.Value;
    }

    private static string RoleName(HouseholdRole role) => role switch
    {
        HouseholdRole.Owner => "owner",
        HouseholdRole.Member => "member",
        _ => "restricted",
    };
}
