using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

namespace FoodAssistant.Application.Households;

/// <summary>
/// 256-bit random tokens, base64url encoded. Stored as SHA-256 hash: the token has full entropy,
/// so a fast hash is enough (no password hashing needed) and allows an indexed lookup.
/// </summary>
public sealed class InvitationTokenService : IInvitationTokenService
{
    private const int TokenBytes = 32;

    public InvitationToken Create()
    {
        var token = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(TokenBytes));
        return new InvitationToken(token, Hash(token));
    }

    public string Hash(string token)
    {
        ArgumentNullException.ThrowIfNull(token);
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    }
}
