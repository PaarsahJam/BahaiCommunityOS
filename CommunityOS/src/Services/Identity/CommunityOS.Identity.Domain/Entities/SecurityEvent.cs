using CommunityOS.SharedKernel.Domain.Primitives;
using CommunityOS.SharedKernel.Guards;

namespace CommunityOS.Identity.Domain.Entities;

/// <summary>
/// An immutable audit record of a security-relevant event for a user
/// account (login success/failure, MFA challenge, recovery, etc.).
/// Stored append-only; never edited or deleted.
/// </summary>
public sealed class SecurityEvent : Entity<Guid>
{
    public Guid UserAccountId { get; private set; }
    public string EventType { get; private set; }
    public string? Description { get; private set; }
    public DateTime OccurredOn { get; private set; }
    public string? IpAddress { get; private set; }
    public string? UserAgent { get; private set; }

    private SecurityEvent(Guid id, Guid userAccountId, string eventType,
        string? description, string? ipAddress, string? userAgent) : base(id)
    {
        UserAccountId = userAccountId;
        EventType = eventType;
        Description = description;
        OccurredOn = DateTime.UtcNow;
        IpAddress = ipAddress;
        UserAgent = userAgent;
    }

    public static SecurityEvent Create(Guid userAccountId, string eventType,
        string? description = null, string? ipAddress = null, string? userAgent = null)
    {
        Guard.NotDefault(userAccountId, nameof(userAccountId));
        Guard.NotNullOrWhiteSpace(eventType, nameof(eventType));
        return new SecurityEvent(Guid.NewGuid(), userAccountId, eventType.Trim(),
            description?.Trim(), ipAddress, userAgent);
    }
}
