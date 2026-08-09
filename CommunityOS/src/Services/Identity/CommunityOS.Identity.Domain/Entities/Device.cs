using CommunityOS.SharedKernel.Domain.Primitives;
using CommunityOS.SharedKernel.Guards;

namespace CommunityOS.Identity.Domain.Entities;

/// <summary>
/// A client device registered against a user account for session
/// management and trusted-device MFA flows.
/// </summary>
public sealed class Device : Entity<Guid>
{
    public string Name { get; private set; }
    public string? Platform { get; private set; }
    public string? UserAgent { get; private set; }
    public DateTime RegisteredOn { get; private set; }
    public DateTime? LastSeenOn { get; private set; }
    public bool IsTrusted { get; private set; }
    public DateTime? TrustedUntil { get; private set; }

    private Device(Guid id, string name, string? platform, string? userAgent) : base(id)
    {
        Name = name;
        Platform = platform;
        UserAgent = userAgent;
        RegisteredOn = DateTime.UtcNow;
        IsTrusted = false;
    }

    public static Device Create(string name, string? platform, string? userAgent)
    {
        Guard.NotNullOrWhiteSpace(name, nameof(name));
        return new Device(Guid.NewGuid(), name.Trim(), platform?.Trim(), userAgent);
    }

    public void MarkSeen()
    {
        LastSeenOn = DateTime.UtcNow;
    }

    public void TrustFor(TimeSpan duration)
    {
        Guard.Positive((int)duration.TotalSeconds, nameof(duration));
        IsTrusted = true;
        TrustedUntil = DateTime.UtcNow.Add(duration);
    }

    public void RevokeTrust()
    {
        IsTrusted = false;
        TrustedUntil = null;
    }

    public bool IsCurrentlyTrusted =>
        IsTrusted && TrustedUntil is { } until && until > DateTime.UtcNow;
}
