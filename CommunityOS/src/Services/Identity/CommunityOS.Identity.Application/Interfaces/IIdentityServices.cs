namespace CommunityOS.Identity.Application.Interfaces;

/// <summary>
/// Issues short-lived access tokens and opaque refresh tokens.
/// Implementations must never log tokens.
/// </summary>
public interface ITokenService
{
    /// <summary>
    /// Issues an access token for the account. <paramref name="tokenFamilyId"/>
    /// is the issuing session's logical session identifier and is carried as the
    /// signed <c>sid</c> claim so the session list can mark the current session.
    /// </summary>
    string GenerateAccessToken(Guid userAccountId, Guid tokenFamilyId, string email);
    string GenerateIdToken(Guid userAccountId, string email, bool emailVerified, string audience, string? nonce);
    string GenerateRefreshToken();
}

/// <summary>
/// Hashes and verifies passwords using a modern, salted, work-factor based
/// algorithm. Implementations must never store or log plaintext passwords.
/// </summary>
public interface IPasswordHasher
{
    string Hash(string plainText);
    bool Verify(string plainText, string hash);
}

/// <summary>
/// RFC 6238 time-based one-time password generation and verification
/// used for authenticator-app MFA.
/// </summary>
public interface ITotpService
{
    string GenerateSecret();
    string ComputeCode(string secret, DateTime timestamp);
    bool Verify(string secret, string code, int acceptedClockDrift = 1);
}

/// <summary>
/// Provides the asymmetric signing key material for access-token issuance
/// and the public JWKS document for key publication.
/// </summary>
public interface ISigningKeyProvider
{
    string SigningAlgorithm { get; }
    string GenerateJwksJson();
}
