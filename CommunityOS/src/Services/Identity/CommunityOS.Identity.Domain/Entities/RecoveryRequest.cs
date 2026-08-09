using CommunityOS.Identity.Domain.Enumerations;
using CommunityOS.SharedKernel.Domain.Primitives;
using CommunityOS.SharedKernel.Guards;

namespace CommunityOS.Identity.Domain.Entities;

/// <summary>
/// A single-use, time-limited recovery request used for password reset
/// or account recovery. The token is stored as a hash; the raw token is
/// only shown to the user once and never persisted or logged.
/// </summary>
public sealed class RecoveryRequest : Entity<Guid>
{
    public Guid UserAccountId { get; private set; }
    public string TokenHash { get; private set; }
    public string Purpose { get; private set; }
    public DateTime RequestedOn { get; private set; }
    public DateTime ExpiresOn { get; private set; }
    public DateTime? ConsumedOn { get; private set; }
    public int Attempts { get; private set; }

    private RecoveryRequest(Guid id, Guid userAccountId, string tokenHash,
        string purpose, DateTime expiresOn) : base(id)
    {
        UserAccountId = userAccountId;
        TokenHash = tokenHash;
        Purpose = purpose;
        RequestedOn = DateTime.UtcNow;
        ExpiresOn = expiresOn;
    }

    public static RecoveryRequest Create(Guid userAccountId, string tokenHash,
        string purpose, TimeSpan lifetime)
    {
        Guard.NotDefault(userAccountId, nameof(userAccountId));
        Guard.NotNullOrWhiteSpace(tokenHash, nameof(tokenHash));
        Guard.NotNullOrWhiteSpace(purpose, nameof(purpose));
        Guard.Positive((int)lifetime.TotalSeconds, nameof(lifetime));
        return new RecoveryRequest(Guid.NewGuid(), userAccountId, tokenHash,
            purpose.Trim(), DateTime.UtcNow.Add(lifetime));
    }

    public bool IsExpired => DateTime.UtcNow >= ExpiresOn;
    public bool IsConsumed => ConsumedOn is not null;

    public bool CanBeConsumed => !IsExpired && !IsConsumed;

    public void Consume()
    {
        if (!CanBeConsumed)
            throw new InvalidOperationException(
                "Recovery request is expired or has already been consumed.");
        ConsumedOn = DateTime.UtcNow;
    }

    public void RecordAttempt() => Attempts++;
}
