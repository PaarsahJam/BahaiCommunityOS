using CommunityOS.Identity.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.Security.Cryptography;
using System.Text.Json;

namespace CommunityOS.Identity.Infrastructure.Security;

/// <summary>
/// Provides the RSA asymmetric signing key used to sign short-lived access
/// tokens and the public JWKS document for key publication. In development a
/// key is generated on startup; in production an externally managed key
/// (PEM or XML) must be supplied via configuration.
/// </summary>
public sealed class RsaSigningKeyProvider : ISigningKeyProvider, IDisposable
{
    public const string Algorithm = SecurityAlgorithms.RsaSha256;

    private readonly RSA _rsa;
    private readonly string _keyId;
    private readonly string? _jwksJson;

    public RsaSigningKeyProvider(IConfiguration configuration)
    {
        _rsa = RSA.Create();
        _keyId = "communityos-signing-key";

        var pem = configuration["Jwt:SigningPrivateKey"];
        var keyXml = configuration["Jwt:SigningKeyXml"];
        var base64 = configuration["Jwt:SigningKeyBase64"];

        if (!string.IsNullOrWhiteSpace(pem))
            _rsa.ImportFromPem(pem);
        else if (!string.IsNullOrWhiteSpace(keyXml))
            _rsa.FromXmlString(keyXml);
        else if (!string.IsNullOrWhiteSpace(base64))
            _rsa.ImportRSAPrivateKey(Convert.FromBase64String(base64), out _);
        else
            _rsa = RSA.Create(2048); // development-only generated key

        _jwksJson = BuildJwksJson();
    }

    public string SigningAlgorithm => Algorithm;

    public RsaSecurityKey SecurityKey
    {
        get
        {
            var key = new RsaSecurityKey(_rsa);
            key.KeyId = _keyId;
            return key;
        }
    }

    public string GenerateJwksJson() => _jwksJson!;

    private string BuildJwksJson()
    {
        var parameters = _rsa.ExportParameters(false);
        var jwk = new
        {
            keys = new[]
            {
                new
                {
                    kty = "RSA",
                    use = "sig",
                    alg = "RS256",
                    kid = _keyId,
                    n = Base64UrlEncode(parameters.Modulus!),
                    e = Base64UrlEncode(parameters.Exponent!)
                }
            }
        };

        return JsonSerializer.Serialize(jwk);
    }

    private static string Base64UrlEncode(byte[] data) =>
        Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public void Dispose() => _rsa.Dispose();
}
