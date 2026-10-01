using CommunityOS.Identity.Application.Interfaces;

namespace CommunityOS.Identity.IntegrationTests.Concurrency;

/// <summary>
/// Deterministic token service for the Q3 concurrency tests. Tokens are opaque
/// strings; only uniquess matter (the refresh family unique index requires each
/// rotated hash to differ). Access-token/JWT material is not validated here, so
/// generation is trivial. PostgreSQL locking/transactions are untouched.
/// </summary>
internal sealed class TestTokenService : ITokenService
{
    public string GenerateAccessToken(
        Guid userAccountId, Guid tokenFamilyId, string email, long sessionRevocationEpoch) =>
        "test-access-token";

    public string GenerateIdToken(
        Guid userAccountId, string email, bool emailVerified, string audience, string? nonce) =>
        "test-id-token";

    // Globally unique across scopes so consecutive rotations never collide on
    // the unique refresh_token_hash index.
    public string GenerateRefreshToken() => $"RT-{Guid.NewGuid():N}";
}