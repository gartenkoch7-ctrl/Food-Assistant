using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace FoodAssistant.Server.Auth;

/// <summary>
/// Registers the mobile app as public OpenID client (code flow + PKCE, refresh tokens) at startup
/// if it does not exist yet. Runs after the database migration (registered later).
/// </summary>
internal sealed class MobileClientRegistration(
    IServiceScopeFactory scopeFactory,
    ILogger<MobileClientRegistration> logger) : IHostedService
{
    public const string ClientId = "foodassistant-mobile";

    /// <summary>Custom-scheme redirect the MAUI app handles per platform (ADR-0003).</summary>
    public static readonly Uri RedirectUri = new("foodassistant://auth/callback");

    public static readonly Uri PostLogoutRedirectUri = new("foodassistant://auth/logout");

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var manager = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
        var existing = await manager.FindByClientIdAsync(ClientId, cancellationToken).ConfigureAwait(false);
        if (existing is null)
        {
            await manager.CreateAsync(CreateDescriptor(), cancellationToken).ConfigureAwait(false);
            AuthLog.ClientRegistered(logger, ClientId);
            return;
        }

        // Overwrite on every start so changed permissions or redirect URIs reach existing installations.
        await manager.UpdateAsync(existing, CreateDescriptor(), cancellationToken).ConfigureAwait(false);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static OpenIddictApplicationDescriptor CreateDescriptor()
    {
        var descriptor = new OpenIddictApplicationDescriptor
        {
            ClientId = ClientId,
            ClientType = ClientTypes.Public,
            ConsentType = ConsentTypes.Implicit,
            DisplayName = "Food-Assistant App",
        };
        descriptor.RedirectUris.Add(RedirectUri);
        descriptor.PostLogoutRedirectUris.Add(PostLogoutRedirectUri);
        descriptor.Permissions.UnionWith(
        [
            Permissions.Endpoints.Authorization,
            Permissions.Endpoints.Token,
            Permissions.Endpoints.EndSession,
            Permissions.Endpoints.Revocation,
            Permissions.GrantTypes.AuthorizationCode,
            Permissions.GrantTypes.RefreshToken,
            Permissions.ResponseTypes.Code,
            Permissions.Scopes.Profile,
            Permissions.Prefixes.Scope + Scopes.OfflineAccess,
        ]);
        descriptor.Requirements.Add(Requirements.Features.ProofKeyForCodeExchange);
        return descriptor;
    }
}
