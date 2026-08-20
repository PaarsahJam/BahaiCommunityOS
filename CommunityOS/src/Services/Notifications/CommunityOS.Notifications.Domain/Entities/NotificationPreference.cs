using CommunityOS.Notifications.Domain.ValueObjects;
using CommunityOS.SharedKernel.Domain.Primitives;
using CommunityOS.SharedKernel.Guards;

namespace CommunityOS.Notifications.Domain.Entities;

/// <summary>
/// A per-member opt-in/opt-out rule for a (type code, channel) pair
/// (ADR-025). The dispatch worker applies preferences when deriving recipients:
/// an opt-out for a (type, channel) suppresses that notification for the member.
/// Preferences are self-managed (relationship tuple) under
/// <c>notifications.preference.manage</c>; members never manage another
/// member's preferences.
/// </summary>
public sealed class NotificationPreference : Entity<Guid>
{
    private NotificationPreference() : base(Guid.Empty)
    {
        TypeCode = null!;
        Channel = NotificationChannel.InApp;
    }

    private NotificationPreference(
        Guid id, Guid memberId, string typeCode, NotificationChannel channel, bool enabled)
        : base(id)
    {
        MemberId = memberId;
        TypeCode = typeCode;
        Channel = channel;
        Enabled = enabled;
    }

    public Guid MemberId { get; private set; }

    public string TypeCode { get; private set; }

    public NotificationChannel Channel { get; private set; }

    /// <summary>True = opted in; false = opted out (suppresses delivery).</summary>
    public bool Enabled { get; private set; }

    public static NotificationPreference Create(
        Guid memberId, string typeCode, NotificationChannel channel, bool enabled)
    {
        Guard.NotDefault(memberId, nameof(memberId));
        Guard.NotNullOrWhiteSpace(typeCode, nameof(typeCode));
        Guard.MaxLength(typeCode, 100, nameof(typeCode));
        Guard.NotNull(channel, nameof(channel));
        return new NotificationPreference(Guid.NewGuid(), memberId, typeCode, channel, enabled);
    }

    public void SetEnabled(bool enabled) => Enabled = enabled;
}