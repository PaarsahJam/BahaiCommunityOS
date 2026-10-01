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
    /// <paramref name="sessionRevocationEpoch"/> is the account's current
    /// ADR-036 session-revocation epoch and is carried as the signed numeric
    /// <c>sre</c> claim.
    /// </summary>
    string GenerateAccessToken(Guid userAccountId, Guid tokenFamilyId, string email, long sessionRevocationEpoch);
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

/// <summary>
/// <para>ADR-036 D3: explicit transaction seam for operations whose
/// accept/reject decision and persistence must commit atomically with the
/// PostgreSQL account-row lock — refresh rotation and emergency session
/// invalidation.</para>
/// <para>The intended protocol is <c>BeginTransactionAsync</c> → update
/// entities through the repositories (each SaveChanges is only a flush inside
/// the transaction) → <c>CommitAsync</c>, which performs exactly one final
/// SaveChanges and the single commit; <c>RollbackAsync</c> (or disposal-less
/// catch path) discards the whole unit.</para>
/// </summary>
public interface IUnitOfWork
{
    /// <summary>Begins an explicit database transaction on the shared context.</summary>
    Task BeginTransactionAsync(CancellationToken ct = default);

    /// <summary>
    /// Flushes all pending entity and outbox state and commits the explicit
    /// transaction — the single transactional persistence boundary.
    /// </summary>
    Task CommitAsync(CancellationToken ct = default);

    /// <summary>Rolls back the explicit transaction, discarding pending changes.</summary>
    Task RollbackAsync(CancellationToken ct = default);
}
