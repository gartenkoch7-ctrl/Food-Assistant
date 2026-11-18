using FoodAssistant.Server.Auth;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.Tokens;

namespace FoodAssistant.Server.Tests.Auth;

/// <summary>Production token keys: created once, stored encrypted, reloaded after a restart.</summary>
public sealed class TokenKeyStoreTests : IDisposable
{
    private readonly DirectoryInfo _directory = Directory.CreateTempSubdirectory("fa-keys-");

    [Fact]
    public void Keys_ArePersistedEncrypted_AndReloaded()
    {
        var dataProtection = DataProtectionProvider.Create(new DirectoryInfo(Path.Combine(_directory.FullName, "ring")));

        using var first = new DataProtectedTokenKeyStore(dataProtection, _directory.FullName, NullLogger<DataProtectedTokenKeyStore>.Instance);
        using var second = new DataProtectedTokenKeyStore(dataProtection, _directory.FullName, NullLogger<DataProtectedTokenKeyStore>.Instance);

        Assert.Equal(Modulus(first.SigningKey), Modulus(second.SigningKey));
        Assert.Equal(Modulus(first.EncryptionKey), Modulus(second.EncryptionKey));
        Assert.NotEqual(Modulus(first.SigningKey), Modulus(first.EncryptionKey));

        var stored = File.ReadAllBytes(Path.Combine(_directory.FullName, "openiddict-signing.key"));
        var plain = ((RsaSecurityKey)first.SigningKey).Rsa.ExportPkcs8PrivateKey();
        Assert.False(stored.AsSpan().SequenceEqual(plain), "The key must not be stored in clear text.");
    }

    public void Dispose() => _directory.Delete(recursive: true);

    private static string Modulus(SecurityKey key) =>
        Convert.ToBase64String(((RsaSecurityKey)key).Rsa.ExportParameters(includePrivateParameters: false).Modulus!);
}
