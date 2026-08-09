using CommunityOS.Identity.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using System.Security.Cryptography;

namespace CommunityOS.Identity.Infrastructure.Security;

/// <summary>
/// Password hashing using PBKDF2 (HMAC-SHA256) with a per-password random
/// salt and a configurable, high iteration count. The output embeds the
/// algorithm version, iteration count and salt so work factors can be
/// raised over time. Plaintext passwords are never retained.
/// </summary>
public sealed class Pbkdf2PasswordHasher(IConfigurationSection? options = null) : IPasswordHasher
{
    private readonly int _iterations = options?.GetValue("Iterations", 210_000) ?? 210_000;
    private const int SaltSize = 16;
    private const int KeySize = 32;
    private const string AlgorithmMarker = "PBKDF2-SHA256";

    public string Hash(string plainText)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(plainText);

        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var key = Rfc2898DeriveBytes.Pbkdf2(plainText, salt, _iterations, HashAlgorithmName.SHA256, KeySize);

        return $"{AlgorithmMarker}${_iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(key)}";
    }

    public bool Verify(string plainText, string hash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(plainText);
        ArgumentException.ThrowIfNullOrWhiteSpace(hash);

        var parts = hash.Split('$');
        if (parts.Length != 4 || parts[0] != AlgorithmMarker)
            return false;

        if (!int.TryParse(parts[1], out var iterations) || iterations < 1)
            return false;

        byte[] salt;
        byte[] expected;
        try
        {
            salt = Convert.FromBase64String(parts[2]);
            expected = Convert.FromBase64String(parts[3]);
        }
        catch (FormatException)
        {
            return false;
        }

        var actual = Rfc2898DeriveBytes.Pbkdf2(plainText, salt, iterations, HashAlgorithmName.SHA256, expected.Length);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }
}
