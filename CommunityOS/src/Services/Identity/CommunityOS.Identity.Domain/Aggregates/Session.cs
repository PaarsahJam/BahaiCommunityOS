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

    /// <summary>
    /// <para>ADR-036 D3: the account's <c>SessionRevocationEpoch</c> observed
    /// when this session was issued (first-party login / OAuth exchange) or
    /// accepted during refresh rotation. Each family is therefore bound to the
    /// epoch under which it was issued.</para>
    /// <para>The refresh decision re-reads the account epoch AFTER acquiring
    /// the account-row lock and rejects a family whose binding is older than
    /// that post-lock value. The pre-lock value is never authoritative; see
    /// <see cref="IsStaleRelativeTo"/> and the refresh command handler.</para>
    /// </summary>
    public long SessionRevocationEpochAtIssue { get; private set; }

    private Session(Guid id, Guid userAccountId, Guid deviceId, Guid tokenFamilyId,
        string refreshTokenHash, string? clientId, long sessionRevocationEpochAtIssue, DateTime expiresOn) : base(id)
    {
        if (sessionRevocationEpochAtIssue < 0)
            throw new ArgumentOutOfRangeException(
                nameof(sessionRevocationEpochAtIssue), "Session revocation epoch cannot be negative.");

        UserAccountId = userAccountId;
        DeviceId = deviceId;
        TokenFamilyId = tokenFamilyId;
        RefreshTokenHash = refreshTokenHash;
        ClientId = clientId;
        SessionRevocationEpochAtIssue = sessionRevocationEpochAtIssue;
        CreatedOn = DateTime.UtcNow;
        LastUsedOn = DateTime.UtcNow;
        ExpiresOn = expiresOn;
        RefreshTokenUsed = false;
    }

    public static Session Create(Guid userAccountId, Guid deviceId,
        string refreshTokenHash, TimeSpan lifetime, string? clientId = null,
        long sessionRevocationEpochAtIssue = 0)
    {
        Guard.NotDefault(userAccountId, nameof(userAccountId));
        Guard.NotDefault(deviceId, nameof(deviceId));
        Guard.NotNullOrWhiteSpace(refreshTokenHash, nameof(refreshTokenHash));
        Guard.Positive((int)lifetime.TotalSeconds, nameof(lifetime));
        var familyId = Guid.NewGuid();
        return new Session(Guid.NewGuid(), userAccountId, deviceId, familyId,
            refreshTokenHash, clientId, sessionRevocationEpochAtIssue, DateTime.UtcNow.Add(lifetime));
    }

    /// <summary>
    /// Instantiates the next token in the same family during rotation.
    /// The previous token hash is superseded by <paramref name="newTokenHash"/>.
    /// The next session binds to <paramref name="sessionRevocationEpochAtIssue"/>
    /// — the account epoch observed under the account-row lock.
    /// </summary>
    public Session Rotate(string newTokenHash, TimeSpan lifetime, long sessionRevocationEpochAtIssue)
    {
        Guard.NotNullOrWhiteSpace(newTokenHash, nameof(newTokenHash));
        Guard.Positive((int)lifetime.TotalSeconds, nameof(lifetime));

        if (RevokedOn is not null)
            throw new InvalidOperationException("Cannot rotate a revoked session.");

        if (DateTime.UtcNow >= ExpiresOn)
            throw new InvalidOperationException("Cannot rotate an expired session.");

        RefreshTokenUsed = true;

        var next = new Session(Guid.NewGuid(), UserAccountId, DeviceId, TokenFamilyId,
            newTokenHash, ClientId, sessionRevocationEpochAtIssue, DateTime.UtcNow.Add(lifetime));
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

    /// <summary>
    /// <para>ADR-036 D3: true when this refresh family is stale — its binding
    /// epoch is older than the account's current <c>SessionRevocationEpoch</c>
    /// — meaning an emergency invalidation committed after the family was
    /// issued/accepted.</para>
    /// <para>MUST be evaluated against the account epoch value observed AFTER
    /// the account-row lock is acquired. A future refactor must not move this
    /// comparison before the lock: the pre-lock epoch cannot see an
    /// invalidation that commits while this operation is waiting for the lock.</para>
    /// </summary>
    public bool IsStaleRelativeTo(long accountSessionRevocationEpoch) =>
        SessionRevocationEpochAtIssue < accountSessionRevocationEpoch;
}
