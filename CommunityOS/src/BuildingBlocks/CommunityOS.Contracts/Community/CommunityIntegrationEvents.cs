namespace CommunityOS.Contracts.Community;

/// <summary>
/// Raised when a Person record is created in the Community service. Carries
/// only a stable id and lifecycle status — never PII (names, contact details
/// or dates of birth are intentionally omitted). Consumers needing profile
/// detail must call the owning service's API.
/// </summary>
public sealed record PersonCreated(Guid PersonId, string Status, DateTime OccurredOn);

/// <summary>
/// Raised when a Person's profile fields change. Minimal data; the profile is
/// owned by the Community service and read through its API.
/// </summary>
public sealed record PersonUpdated(Guid PersonId, string Status, DateTime OccurredOn);

/// <summary>
/// Raised when a Person is deactivated. Deactivation preserves historical
/// community participation; it is a lifecycle state, not deletion.
/// </summary>
public sealed record PersonDeactivated(Guid PersonId, DateTime OccurredOn);

/// <summary>
/// Raised when a Person is linked to an Identity account. The link is
/// explicit and auditable. Identity remains authoritative for the account; the
/// link is owned by the Community service.
/// </summary>
public sealed record PersonIdentityLinked(Guid PersonId, Guid IdentityAccountId, DateTime OccurredOn);

/// <summary>
/// Raised when the link between a Person and an Identity account is removed.
/// </summary>
public sealed record PersonIdentityUnlinked(Guid PersonId, Guid IdentityAccountId, DateTime OccurredOn);

/// <summary>
/// Raised when a community membership is created or transitions to a new
/// status. Membership is a Community concern and is never represented by an
/// authorization role.
/// </summary>
public sealed record MembershipChanged(
    Guid MembershipId,
    Guid PersonId,
    string Status,
    DateTime EffectiveFrom,
    DateTime? EffectiveUntil,
    DateTime OccurredOn);

/// <summary>
/// Raised when a household is created. Minimal data; household composition is
/// read through the Community API.
/// </summary>
public sealed record HouseholdCreated(Guid HouseholdId, DateTime OccurredOn);

/// <summary>
/// Raised when a community activity is created. Activities are Community
/// capabilities; scheduling stays inside the Community service.
/// </summary>
public sealed record ActivityCreated(Guid ActivityId, Guid? OrganizationUnitId, string Status, DateTime OccurredOn);

/// <summary>
/// Raised when a community activity's details or status change.
/// </summary>
public sealed record ActivityUpdated(Guid ActivityId, Guid? OrganizationUnitId, string Status, DateTime OccurredOn);

/// <summary>
/// Raised when a community event is created. Events are Community-owned and
/// separate from the future Records/Calendar services.
/// </summary>
public sealed record CommunityEventCreated(
    Guid EventId,
    string Title,
    DateTime StartsAt,
    Guid? OrganizationUnitId,
    string Status,
    DateTime OccurredOn);

/// <summary>
/// Raised when a community event is updated or rescheduled.
/// </summary>
public sealed record CommunityEventUpdated(
    Guid EventId,
    DateTime StartsAt,
    Guid? OrganizationUnitId,
    string Status,
    DateTime OccurredOn);

/// <summary>
/// Raised when a meeting is created. Meeting minutes are operational drafts,
/// not official records (Records service owns formal records).
/// </summary>
public sealed record MeetingCreated(
    Guid MeetingId,
    string Title,
    DateTime StartsAt,
    Guid? OrganizationUnitId,
    string Status,
    DateTime OccurredOn);

/// <summary>
/// Raised when a meeting's minutes/actions are recorded.
/// </summary>
public sealed record MeetingRecorded(Guid MeetingId, Guid? OrganizationUnitId, DateTime OccurredOn);

/// <summary>
/// Raised when community participation is recorded (activity, event, meeting
/// or volunteer service). Carries the person id and target, never PII.
/// </summary>
public sealed record ParticipationRecorded(
    Guid ParticipationId,
    Guid PersonId,
    string TargetType,
    Guid TargetId,
    string Status,
    DateTime OccurredOn);
