using FoodAssistant.Application.Accounts;
using Microsoft.AspNetCore.Identity;

namespace FoodAssistant.Infrastructure.Accounts;

/// <summary>Account protection rules of ADR-0003.</summary>
internal static class IdentityPolicy
{
    /// <summary>Claim type of the user id in cookies and tokens alike (OIDC <c>sub</c>).</summary>
    public const string UserIdClaimType = "sub";

    public static void Configure(IdentityOptions options)
    {
        // Length over complexity: at least 10 characters, no character-class requirements.
        options.Password.RequiredLength = AccountValidation.PasswordMinLength;
        options.Password.RequireDigit = false;
        options.Password.RequireLowercase = false;
        options.Password.RequireUppercase = false;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequiredUniqueChars = 1;

        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        options.Lockout.AllowedForNewUsers = true;

        // There are no e-mails: the user name is the login, nothing needs confirming.
        options.User.RequireUniqueEmail = false;
        options.SignIn.RequireConfirmedAccount = false;
        options.SignIn.RequireConfirmedEmail = false;
        options.SignIn.RequireConfirmedPhoneNumber = false;

        options.ClaimsIdentity.UserIdClaimType = UserIdClaimType;
    }
}
