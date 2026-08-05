using CommunityOS.SharedKernel.Domain.Primitives;
using CommunityOS.SharedKernel.Guards;

namespace CommunityOS.Identity.Domain.Entities;

public sealed class RefreshToken : Entity<Guid>
{
    public Guid MemberId { get; }
    public string Token { get; }
    public DateTime ExpiresAt { get; }
    public DateTime CreatedAt { get; }
    public bool IsRevoked { get; private set; }
    public DateTime? RevokedAt { get; private set; }

    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;
    public bool IsActive  => !IsRevoked && !IsExpired;

    private RefreshToken(Guid id, Guid memberId, string token, DateTime expiresAt) : base(id)
    {
        MemberId  = memberId;
        Token     = token;
        ExpiresAt = expiresAt;
        CreatedAt = DateTime.UtcNow;
    }

    public static RefreshToken Create(Guid memberId, string token, int lifetimeDays = 30)
    {
        Guard.NotDefault(memberId, nameof(memberId));
        Guard.NotNullOrWhiteSpace(token, nameof(token));
        Guard.Positive(lifetimeDays, nameof(lifetimeDays));
        return new RefreshToken(Guid.NewGuid(), memberId, token,
            DateTime.UtcNow.AddDays(lifetimeDays));
    }

    public void Revoke()
    {
        IsRevoked = true;
        RevokedAt = DateTime.UtcNow;
    }
}
