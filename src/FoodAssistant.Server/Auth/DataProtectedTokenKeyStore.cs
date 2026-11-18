using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.IdentityModel.Tokens;

namespace FoodAssistant.Server.Auth;

/// <summary>
/// RSA keys for OpenIddict, created on first start and stored next to the Data Protection key ring
/// (<c>FoodAssistant:DataProtectionPath</c>), encrypted with Data Protection (ADR-0007). Losing the
/// volume invalidates issued tokens: the app then simply signs in again.
/// </summary>
internal sealed class DataProtectedTokenKeyStore : ITokenKeyStore, IDisposable
{
    private const string ProtectorPurpose = "FoodAssistant.OpenIddict.TokenKeys.v1";
    private const int KeySizeInBits = 3072;

    private readonly RSA _signing;
    private readonly RSA _encryption;

    public DataProtectedTokenKeyStore(
        IDataProtectionProvider dataProtection,
        string directory,
        ILogger<DataProtectedTokenKeyStore> logger)
    {
        ArgumentNullException.ThrowIfNull(dataProtection);
        Directory.CreateDirectory(directory);
        var protector = dataProtection.CreateProtector(ProtectorPurpose);

        _signing = LoadOrCreate(protector, Path.Combine(directory, "openiddict-signing.key"), "signing", logger);
        _encryption = LoadOrCreate(protector, Path.Combine(directory, "openiddict-encryption.key"), "encryption", logger);
        SigningKey = new RsaSecurityKey(_signing);
        EncryptionKey = new RsaSecurityKey(_encryption);
    }

    public SecurityKey SigningKey { get; }

    public SecurityKey EncryptionKey { get; }

    public void Dispose()
    {
        _signing.Dispose();
        _encryption.Dispose();
    }

    private static RSA LoadOrCreate(IDataProtector protector, string path, string purpose, ILogger logger)
    {
        var rsa = RSA.Create();
        try
        {
            if (File.Exists(path))
            {
                rsa.ImportPkcs8PrivateKey(protector.Unprotect(File.ReadAllBytes(path)), out _);
                return rsa;
            }

            rsa.KeySize = KeySizeInBits;
            var protectedKey = protector.Protect(rsa.ExportPkcs8PrivateKey());

            // Write to a temporary file first so a crash never leaves a half-written key behind.
            var temporaryPath = path + ".tmp";
            File.WriteAllBytes(temporaryPath, protectedKey);
            File.Move(temporaryPath, path, overwrite: false);
            AuthLog.TokenKeyCreated(logger, purpose);
            return rsa;
        }
        catch
        {
            rsa.Dispose();
            throw;
        }
    }
}
