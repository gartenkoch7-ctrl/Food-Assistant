using FoodAssistant.Server.Auth;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Server;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace FoodAssistant.Server.Composition;

/// <summary>
/// OpenIddict as own OpenID provider for the app (ADR-0003): authorization code + PKCE, refresh
/// tokens, access tokens validated locally for <c>/api/v1</c>.
/// </summary>
public static class TokenServerRegistration
{
    /// <summary>Public address (env: <c>FoodAssistant__PublicUrl</c>); becomes the token issuer (ADR-0007).</summary>
    public const string PublicUrlKey = "FoodAssistant:PublicUrl";

    public static WebApplicationBuilder AddTokenServer(this WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        var services = builder.Services;
        var isDevelopment = builder.Environment.IsDevelopment();
        var publicUrl = builder.Configuration[PublicUrlKey];
        if (!isDevelopment && !Uri.TryCreate(publicUrl, UriKind.Absolute, out _))
        {
            // Without a fixed issuer it would follow the Host header, and tokens would break as soon
            // as the app reaches the server under another name (ADR-0007: the public URL is mandatory).
            throw new InvalidOperationException(
                "FoodAssistant__PublicUrl is not set or not an absolute URL (e.g. https://food.example.org).");
        }

        services.AddOpenIddict()
            .AddServer(options =>
            {
                options.SetAuthorizationEndpointUris("connect/authorize")
                    .SetTokenEndpointUris("connect/token")
                    .SetEndSessionEndpointUris("connect/endsession")
                    .SetRevocationEndpointUris("connect/revoke");

                options.AllowAuthorizationCodeFlow().AllowRefreshTokenFlow();
                options.RequireProofKeyForCodeExchange();
                options.RegisterScopes(Scopes.OpenId, Scopes.Profile, Scopes.OfflineAccess);

                // Refresh tokens roll (new token on every use, the old one is redeemed) and expire
                // sliding – both OpenIddict defaults, kept on purpose (ADR-0003). The default reuse
                // leeway of 30 s stays too: a phone that lost the refresh response can retry.
                options.SetAccessTokenLifetime(TimeSpan.FromMinutes(15));
                options.SetRefreshTokenLifetime(TimeSpan.FromDays(90));

                if (!string.IsNullOrWhiteSpace(publicUrl))
                {
                    options.SetIssuer(new Uri(publicUrl, UriKind.Absolute));
                }

                if (isDevelopment)
                {
                    // Development and tests: keys live in memory, tokens die with the process.
                    options.AddEphemeralEncryptionKey().AddEphemeralSigningKey();
                }

                var aspNetCore = options.UseAspNetCore()
                    .EnableAuthorizationEndpointPassthrough()
                    .EnableTokenEndpointPassthrough()
                    .EnableEndSessionEndpointPassthrough();
                if (isDevelopment)
                {
                    // TLS ends at the reverse proxy in production (ADR-0007); locally plain HTTP is fine.
                    aspNetCore.DisableTransportSecurityRequirement();
                }
            })
            .AddValidation(options =>
            {
                options.UseLocalServer();
                options.UseAspNetCore();
            });

        if (!isDevelopment)
        {
            AddPersistentKeys(builder);
        }

        services.AddHostedService<MobileClientRegistration>();
        return builder;
    }

    private static void AddPersistentKeys(WebApplicationBuilder builder)
    {
        var keyPath = builder.Configuration[SessionAuthenticationRegistration.DataProtectionPathKey];
        if (string.IsNullOrWhiteSpace(keyPath))
        {
            throw new InvalidOperationException(
                "FoodAssistant__DataProtectionPath is not set. Point it to a persistent volume: it holds the "
                + "key ring for sessions, stored secrets and token signing (ADR-0007).");
        }

        builder.Services.AddSingleton<ITokenKeyStore>(services => new DataProtectedTokenKeyStore(
            services.GetRequiredService<IDataProtectionProvider>(),
            keyPath,
            services.GetRequiredService<ILogger<DataProtectedTokenKeyStore>>()));

        builder.Services.AddOptions<OpenIddictServerOptions>().Configure<ITokenKeyStore>((options, keys) =>
        {
            options.SigningCredentials.Add(new SigningCredentials(keys.SigningKey, SecurityAlgorithms.RsaSha256));
            options.EncryptionCredentials.Add(new EncryptingCredentials(
                keys.EncryptionKey,
                SecurityAlgorithms.RsaOAEP,
                SecurityAlgorithms.Aes256CbcHmacSha512));
        });
    }
}
