using System.Text;
using FoodAssistant.Application.Accounts;
using Microsoft.AspNetCore.Identity;

namespace FoodAssistant.Infrastructure.Accounts;

/// <summary>
/// Turns Identity errors into stable field error codes (e.g. <c>password: password_too_short</c>).
/// Identity's descriptions are not passed on: they are English only and may echo the user name.
/// </summary>
internal static class IdentityErrorMapping
{
    public static ProvisioningResult ToResult(IdentityResult result)
    {
        if (result.Errors.Any(e => e.Code == nameof(IdentityErrorDescriber.DuplicateUserName)))
        {
            return ProvisioningResult.Failure(ProvisioningStatus.UserNameTaken);
        }

        var errors = result.Errors
            .GroupBy(e => FieldOf(e.Code), StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Select(e => ToSnakeCase(e.Code)).Distinct().ToArray(), StringComparer.Ordinal);
        return ProvisioningResult.Invalid(errors);
    }

    public static IEnumerable<string> Codes(IdentityResult result) => result.Errors.Select(e => e.Code);

    private static string FieldOf(string code) =>
        code.StartsWith("Password", StringComparison.Ordinal) ? "password"
        : code.Contains("UserName", StringComparison.Ordinal) ? "userName"
        : "account";

    private static string ToSnakeCase(string code)
    {
        var builder = new StringBuilder(code.Length + 8);
        for (var i = 0; i < code.Length; i++)
        {
            var c = code[i];
            if (char.IsUpper(c) && i > 0)
            {
                builder.Append('_');
            }

            builder.Append(char.ToLowerInvariant(c));
        }

        return builder.ToString();
    }
}
