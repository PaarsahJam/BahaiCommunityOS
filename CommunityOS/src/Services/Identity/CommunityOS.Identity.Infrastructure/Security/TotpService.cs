using CommunityOS.Identity.Application.Interfaces;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Security.Cryptography;

namespace CommunityOS.Identity.Infrastructure.Security;

/// <summary>
/// RFC 6238 time-based one-time password (TOTP) implementation used for
/// authenticator-app MFA. Supports a configurable time step and validates
/// codes within a small clock-drift window. Shared secrets must never be
/// logged.
/// </summary>
public sealed class TotpService(int timeStepSeconds = 30, int digits = 6) : ITotpService
{
    public string GenerateSecret()
    {
        var key = RandomNumberGenerator.GetBytes(20);
        return Base32Encode(key);
    }

    [SuppressMessage("Security", "CA5350:Do Not Use Weak Cryptographic Algorithms",
        Justification = "RFC 6238 TOTP mandates HMAC-SHA1 for interoperability with standard authenticator apps.")]
    public string ComputeCode(string secret, DateTime timestamp)
    {
        var key = Base32Decode(secret);
        var counter = (long)Math.Floor(
            (timestamp.ToUniversalTime() - DateTime.UnixEpoch).TotalSeconds / timeStepSeconds);
        var counterBytes = BitConverter.GetBytes(counter);
        if (BitConverter.IsLittleEndian)
            Array.Reverse(counterBytes);

        using var hmac = new HMACSHA1(key);
        var hash = hmac.ComputeHash(counterBytes);
        var offset = hash[^1] & 0x0F;
        var binary = (hash[offset] & 0x7F) << 24
                     | (hash[offset + 1] & 0xFF) << 16
                     | (hash[offset + 2] & 0xFF) << 8
                     | (hash[offset + 3] & 0xFF);
        var otp = binary % (int)Math.Pow(10, digits);
        return otp.ToString($"D{digits}", CultureInfo.InvariantCulture);
    }

    public bool Verify(string secret, string code, int acceptedClockDrift = 1)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentOutOfRangeException.ThrowIfNegative(acceptedClockDrift);

        var now = DateTime.UtcNow;
        for (var drift = -acceptedClockDrift; drift <= acceptedClockDrift; drift++)
        {
            var candidate = ComputeCode(secret, now.AddSeconds(drift * timeStepSeconds));
            if (CryptographicOperations.FixedTimeEquals(
                System.Text.Encoding.UTF8.GetBytes(candidate),
                System.Text.Encoding.UTF8.GetBytes(code)))
            {
                return true;
            }
        }
        return false;
    }

    private static string Base32Encode(byte[] data)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var output = new System.Text.StringBuilder();
        int bits = 0, value = 0;
        foreach (var b in data)
        {
            value = (value << 8) | b;
            bits += 8;
            while (bits >= 5)
            {
                output.Append(alphabet[(value >> (bits - 5)) & 0x1F]);
                bits -= 5;
            }
        }
        if (bits > 0)
            output.Append(alphabet[(value << (5 - bits)) & 0x1F]);
        return output.ToString();
    }

    private static byte[] Base32Decode(string encoded)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var cleaned = new string(encoded.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
        var output = new List<byte>();
        int bits = 0, value = 0;
        foreach (var c in cleaned)
        {
            var index = alphabet.IndexOf(c);
            if (index < 0) throw new ArgumentException("Invalid Base32 string.", nameof(encoded));
            value = (value << 5) | index;
            bits += 5;
            if (bits >= 8)
            {
                output.Add((byte)((value >> (bits - 8)) & 0xFF));
                bits -= 8;
            }
        }
        return [.. output];
    }
}
