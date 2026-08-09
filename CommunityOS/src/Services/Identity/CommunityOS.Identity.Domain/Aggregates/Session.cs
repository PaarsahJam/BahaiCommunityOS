using CommunityOS.SharedKernel.Domain.Primitives;
using CommunityOS.SharedKernel.Guards;

namespace CommunityOS.Identity.Domain.Aggregates;

/// <summary>
/// An authenticated client session. Encapsulates refresh-token rotation:
/// each use rotates the token within a single token family. Reuse of an
/// already-rotated token indicates theft and revokes the whole family.
/// The raw refresh token is never persisted — only its hash is stored.
/// </summary>
public sealed class Session : AggregateRoot<Guid>
{
    public Guid UserAccountId { get; private set; }
    public Guid DeviceId { get; private set; }
    public Guid TokenFamilyId { get; private set; }
    public string RefreshTokenHash { get; private set; }
    /// <summary>
    /// Public client identifier this session was issued to, when issued via
    /// the OAuth token endpoint. Null for first-party login sessions. Used
    /// to enforce that a refresh token can only be rotated by its owning client.
    /// </summary>
    public string? ClientId { get; private set; }
    public DateTime CreatedOn { get; private set; }
    public DateTime ExpiresOn { get; private set; }
    public DateTime LastUsedOn { get; private set; }
    public DateTime? RevokedOn { get; private set; }
    public string? RevocationReason { get; private set; }
    public bool RefreshTokenUsed { get; private set; }

    private Session(Guid id, Guid userAccountId, Guid deviceId, Guid tokenFamilyId,
        string refreshTokenHash, string? clientId, DateTime expiresOn) : base(id)
    {
        UserAccountId = userAccountId;
        DeviceId = deviceId;
        TokenFamilyId = tokenFamilyId;
        RefreshTokenHash = refreshTokenHash;
        ClientId = clientId;
        CreatedOn = DateTime.UtcNow;
        LastUsedOn = DateTime.UtcNow;
        ExpiresOn = expiresOn;
        RefreshTokenUsed = false;
    }

    public static Session Create(Guid userAccountId, Guid deviceId,
        string refreshTokenHash, TimeSpan lifetime, string? clientId = null)
    {
        Guard.NotDefault(userAccountId, nameof(userAccountId));
        Guard.NotDefault(deviceId, nameof(deviceId));
        Guard.NotNullOrWhiteSpace(refreshTokenHash, nameof(refreshTokenHash));
        Guard.Positive((int)lifetime.TotalSeconds, nameof(lifetime));
        var familyId = Guid.NewGuid();
        return new Session(Guid.NewGuid(), userAccountId, deviceId, familyId,
            refreshTokenHash, clientId, DateTime.UtcNow.Add(lifetime));
    }

    /// <summary>
    /// Instantiates the next token in the same family during rotation.
    /// The previous token hash is superseded by <paramref name="newTokenHash"/>.
    /// </summary>
    public Session Rotate(string newTokenHash, TimeSpan lifetime)
    {
        Guard.NotNullOrWhiteSpace(newTokenHash, nameof(newTokenHash));
        Guard.Positive((int)lifetime.TotalSeconds, nameof(lifetime));

        if (RevokedOn is not null)
            throw new InvalidOperationException("Cannot rotate a revoked session.");

        if (DateTime.UtcNow >= ExpiresOn)
            throw new InvalidOperationException("Cannot rotate an expired session.");

        RefreshTokenUsed = true;

        var next = new Session(Guid.NewGuid(), UserAccountId, DeviceId, TokenFamilyId,
            newTokenHash, ClientId, DateTime.UtcNow.Add(lifetime));
        return next;
    }

    /// <summary>
    /// Records a refresh-token reuse, which indicates the token may have been
    /// stolen. This session and every session in the family must be revoked.
    /// </summary>
    public void MarkReused()
    {
        RefreshTokenUsed = true;
        Revoke("Refresh token reuse detected.");
    }

    public bool IsRevoked => RevokedOn is not null;
    public bool IsExpired => DateTime.UtcNow >= ExpiresOn;
    public bool IsActive => !IsRevoked && !IsExpired;

    public void Revoke(string reason)
    {
        if (RevokedOn is not null) return;
        RevokedOn = DateTime.UtcNow;
        RevocationReason = reason;
    }

    public void Touch() => LastUsedOn = DateTime.UtcNow;
}
