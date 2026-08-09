using CommunityOS.Identity.Domain.Aggregates;
using System.Security.Cryptography;
using System.Text;

namespace CommunityOS.Identity.Application.Crypto;

/// <summary>
/// RFC 7636 Proof Key for Code Exchange helpers: verifier generation,
/// S256 challenge derivation, and constant-time verification. Used to bind
/// an authorization code to a public client that proves possession of the
/// verifier when exchanging the code for tokens.
/// </summary>
public static class Pkce
{
    private const string VerifierCharset = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-._~";

    public static string GenerateVerifier(int entropyBytes = 32)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(entropyBytes);
        return Base64UrlEncode(RandomNumberGenerator.GetBytes(entropyBytes));
    }

    public static string ComputeChallengeS256(string codeVerifier)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(codeVerifier);
        var challenge = SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier));
        return Base64UrlEncode(challenge);
    }

    public static bool IsSupportedMethod(string codeChallengeMethod) =>
        codeChallengeMethod is AuthorizationCode.ChallengeMethodS256
            or AuthorizationCode.ChallengeMethodPlain;

    public static bool IsValidVerifierFormat(string codeVerifier) =>
        codeVerifier.Length is >= 43 and <= 128 &&
        codeVerifier.All(c => VerifierCharset.Contains(c));

    public static bool Verify(string codeVerifier, string codeChallenge, string codeChallengeMethod)
    {
        if (string.IsNullOrWhiteSpace(codeVerifier) || string.IsNullOrWhiteSpace(codeChallenge))
            return false;

        string? expected = codeChallengeMethod switch
        {
            AuthorizationCode.ChallengeMethodS256 => ComputeChallengeS256(codeVerifier),
            AuthorizationCode.ChallengeMethodPlain => codeVerifier,
            _ => null
        };

        return expected is not null &&
               FixedTimeEquals(expected, codeChallenge);
    }

    private static bool FixedTimeEquals(string left, string right)
    {
        var a = Encoding.UTF8.GetBytes(left);
        var b = Encoding.UTF8.GetBytes(right);
        return CryptographicOperations.FixedTimeEquals(a, b);
    }

    private static string Base64UrlEncode(byte[] data) =>
        Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
