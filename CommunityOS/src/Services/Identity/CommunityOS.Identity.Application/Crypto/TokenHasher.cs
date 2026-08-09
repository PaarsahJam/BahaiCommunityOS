using System.Security.Cryptography;
using System.Text;

namespace CommunityOS.Identity.Application.Crypto;

/// <summary>
/// Produces irreversible SHA-256 hashes of opaque tokens (refresh tokens,
/// recovery tokens) so the raw values are never persisted. Hashes only —
/// never plaintext tokens, which are the responsibility of callers.
/// </summary>
public static class TokenHasher
{
    public static string Hash(string token)
    {
        ArgumentNullException.ThrowIfNull(token);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes);
    }

    public static string GenerateToken(int bytes = 32)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(bytes);
        return Convert.ToBase64String(RandomNumberGenerator.GetBytes(bytes));
    }
}
