using FoodAssistant.Infrastructure.Accounts;
using FoodAssistant.Server.Auth;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using OpenIddict.Validation.AspNetCore;

namespace FoodAssistant.Server.Composition;

/// <summary>
/// Data Protection, the web session cookie (BFF, ADR-0003) and the cookie-or-bearer default scheme.
/// </summary>
public static class SessionAuthenticationRegistration
{
    /// <summary>Directory of the Data Protection key ring (env: <c>FoodAssistant__DataProtectionPath</c>, a volume in the container).</summary>
    public const string DataProtectionPathKey = "FoodAssistant:DataProtectionPath";

    public static WebApplicationBuilder AddSessionAuthentication(this WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        var services = builder.Services;

        var dataProtection = services.AddDataProtection().SetApplicationName("FoodAssistant");
        var keyPath = builder.Configuration[DataProtectionPathKey];
        if (!string.IsNullOrWhiteSpace(keyPath))
        {
            dataProtection.PersistKeysToFileSystem(new DirectoryInfo(keyPath));
        }

        new IdentityBuilder(typeof(User), services).AddSignInManager();

        services.AddAuthentication(options =>
            {
                options.DefaultScheme = AuthSchemes.CookieOrBearer;
                options.DefaultChallengeScheme = AuthSchemes.CookieOrBearer;
            })
            .AddPolicyScheme(AuthSchemes.CookieOrBearer, "Cookie or bearer token", options =>
                options.ForwardDefaultSelector = SelectScheme)
            .AddIdentityCookies();

        services.ConfigureApplicationCookie(options =>
        {
            // __Host- prefix: only over HTTPS, no Domain attribute, path "/" – cannot be planted by subdomains.
            options.Cookie.Name = "__Host-FoodAssistant.Session";
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.ExpireTimeSpan = TimeSpan.FromDays(14);
            options.SlidingExpiration = true;

            // An API answers with status codes; there is no login page to redirect to (yet).
            options.Events.OnRedirectToLogin = context =>
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            };
            options.Events.OnRedirectToAccessDenied = context =>
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            };
        });

        services.AddAntiforgery(options =>
        {
            options.HeaderName = CookieAntiforgeryFilter.HeaderName;
            options.Cookie.Name = "__Host-FoodAssistant.Antiforgery";
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.Cookie.SameSite = SameSiteMode.Strict;
        });

        return builder;
    }

    private static string SelectScheme(HttpContext context)
    {
        string? authorization = context.Request.Headers.Authorization;
        return authorization?.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) == true
            ? OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme
            : IdentityConstants.ApplicationScheme;
    }
}
