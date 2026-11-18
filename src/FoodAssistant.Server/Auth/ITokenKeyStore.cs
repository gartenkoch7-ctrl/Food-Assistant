using Microsoft.IdentityModel.Tokens;

namespace FoodAssistant.Server.Auth;

/// <summary>Persistent keys that sign and encrypt OpenIddict tokens in production.</summary>
public interface ITokenKeyStore
{
    SecurityKey SigningKey { get; }

    SecurityKey EncryptionKey { get; }
}
